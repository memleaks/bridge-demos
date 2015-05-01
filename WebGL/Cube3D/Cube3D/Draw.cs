﻿using Bridge;
using Bridge.Html5;
using Bridge.WebGL;
using Html5.TypedArrays;
using System;

namespace Cube3D
{
    public class App
    {
        [Ready]
        public static void Main()
        {
            var cube = new Cube();

            cube.canvas = App.GetCanvasEl("lesson07-canvas");
            cube.gl = App.InitGL(cube.canvas);
            cube.InitShaders();
            cube.InitBuffers();
            cube.InitTexture();

            cube.gl.ClearColor(1, 1, 1, 1);
            cube.gl.Enable(cube.gl.DEPTH_TEST);

            Document.OnKeyDown = cube.HandleKeyDown;
            Document.OnKeyUp = cube.HandleKeyUp;

            cube.Tick();
        }

        public static CanvasElement GetCanvasEl(string id)
        {
            return Document.GetElementById(id).As<CanvasElement>();
        }

        public static WebGLRenderingContext InitGL(CanvasElement canvas)
        {
            var gl = App.Create3DContext(canvas);

            if (gl == null)
            {
                Global.Alert("Could not initialise WebGL, sorry :-(");
            }

            return gl;
        }

        public static WebGLRenderingContext Create3DContext(CanvasElement canvas)
        {
            string[] names = new string[] 
            { 
                "webgl", 
                "experimental-webgl", 
                "webkit-3d", 
                "moz-webgl" 
            };

            WebGLRenderingContext context = null;

            foreach (string name in names)
            {
                try
                {
                    context = canvas.GetContext(name).As<WebGLRenderingContext>();
                }
                catch (Exception ex) { }

                if (context != null)
                {
                    break;
                }
            }

            return context;
        }
    }

    public class Cube
    {
        public CanvasElement canvas;
        public WebGLRenderingContext gl;
        public WebGLProgram program;
        public WebGLTexture texture;

        public double[] mvMatrix = Script.Call<double[]>("mat4.create");
        public double[][] mvMatrixStack = new double[][] { };
        public double[] pMatrix = Script.Call<double[]>("mat4.create");

        public int vertexPositionAttribute;
        public int vertexNormalAttribute;
        public int textureCoordAttribute;

        public WebGLUniformLocation pMatrixUniform;
        public WebGLUniformLocation mvMatrixUniform;
        public WebGLUniformLocation nMatrixUniform;
        public WebGLUniformLocation samplerUniform;

        public WebGLBuffer cubeVertexPositionBuffer;
        public WebGLBuffer cubeVertexNormalBuffer;
        public WebGLBuffer cubeVertexTextureCoordBuffer;
        public WebGLBuffer cubeVertexIndexBuffer;

        public double xRot = 0;
        public int xSpeed = 3;

        public double yRot = 0;
        public int ySpeed = -3;

        public double z = -5.0;
        public bool[] currentlyPressedKeys = new bool[] { };

        public double lastTime = 0;

        public WebGLShader GetShader(WebGLRenderingContext gl, string id)
        {
            var shaderScript = Document.GetElementById(id).As<ScriptElement>();

            if (shaderScript == null)
            {
                return null;
            }

            var str = "";
            var k = shaderScript.FirstChild;

            while (k != null)
            {
                if (k.NodeType == NodeType.Text)
                {
                    str += k.TextContent;
                }

                k = k.NextSibling;
            }

            WebGLShader shader;

            if (shaderScript.Type == "x-shader/x-fragment")
            {
                shader = gl.CreateShader(gl.FRAGMENT_SHADER);
            }
            else if (shaderScript.Type == "x-shader/x-vertex")
            {
                shader = gl.CreateShader(gl.VERTEX_SHADER);
            }
            else
            {
                return null;
            }

            gl.ShaderSource(shader, str);
            gl.CompileShader(shader);

            if (!gl.GetShaderParameter(shader, gl.COMPILE_STATUS).As<bool>())
            {
                Global.Alert(gl.GetShaderInfoLog(shader));
                return null;
            }

            return shader;
        }

        WebGLProgram shaderProgram;

        public void InitShaders()
        {
            var fragmentShader = this.GetShader(gl, "shader-fs");
            var vertexShader = this.GetShader(gl, "shader-vs");

            shaderProgram = gl.CreateProgram().As<WebGLProgram>();

            if (shaderProgram.Is<int>())
            {
                Global.Alert("Could not initialise program");
            }

            gl.AttachShader(shaderProgram, vertexShader);
            gl.AttachShader(shaderProgram, fragmentShader);
            gl.LinkProgram(shaderProgram);

            if (!gl.GetProgramParameter(shaderProgram, gl.LINK_STATUS).As<bool>())
            {
                Global.Alert("Could not initialise shaders");
            }

            gl.UseProgram(shaderProgram);

            this.vertexPositionAttribute = gl.GetAttribLocation(shaderProgram, "aVertexPosition");
            this.vertexNormalAttribute = gl.GetAttribLocation(shaderProgram, "aVertexNormal");
            this.textureCoordAttribute = gl.GetAttribLocation(shaderProgram, "aTextureCoord");

            gl.EnableVertexAttribArray(this.vertexPositionAttribute);
            gl.EnableVertexAttribArray(this.vertexNormalAttribute);
            gl.EnableVertexAttribArray(this.textureCoordAttribute);

            this.pMatrixUniform = gl.GetUniformLocation(shaderProgram, "uPMatrix");
            this.mvMatrixUniform = gl.GetUniformLocation(shaderProgram, "uMVMatrix");
            this.nMatrixUniform = gl.GetUniformLocation(shaderProgram, "uNMatrix");
            this.samplerUniform = gl.GetUniformLocation(shaderProgram, "uSampler");

            this.program = shaderProgram;
        }


        public void HandleLoadedTexture(ImageElement image)
        {
            gl.PixelStorei(gl.UNPACK_FLIP_Y_WEBGL, gl.ONE);
            //Script.Call("gl.pixelStorei", gl.UNPACK_FLIP_Y_WEBGL, true);

            gl.BindTexture(gl.TEXTURE_2D, this.texture);
            gl.TexImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, image);
            gl.TexParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
            gl.TexParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_NEAREST);
            gl.GenerateMipmap(gl.TEXTURE_2D);
            gl.BindTexture(gl.TEXTURE_2D, null);
        }

        public void InitTexture()
        {
            this.texture = gl.CreateTexture();

            var textureImageElement = new ImageElement();

            textureImageElement.OnLoad = (ev) =>
            {
                this.HandleLoadedTexture(textureImageElement);
            };

            textureImageElement.Src = "crate.gif";
        }

        public void MVPushMatrix()
        {
            var copy = Script.Call<object>("mat4.create");
            Script.Call("mat4.set", mvMatrix, copy);

            this.mvMatrixStack.Push(copy);
        }

        public void MVPopMatrix()
        {
            if (mvMatrixStack.Length == 0)
            {
                throw new Exception("Invalid popMatrix!");
            }

            this.mvMatrix = mvMatrixStack.Pop().As<double[]>();
        }

        public void SetMatrixUniforms()
        {
            gl.UniformMatrix4fv(this.pMatrixUniform, false, pMatrix);
            gl.UniformMatrix4fv(this.mvMatrixUniform, false, mvMatrix);

            var normalMatrix = Script.Call<double[]>("mat3.create");

            Script.Call<object>("mat4.toInverseMat3", mvMatrix, normalMatrix);
            Script.Call<object>("mat3.transpose", normalMatrix);

            gl.UniformMatrix3fv(this.nMatrixUniform, false, normalMatrix);
        }

        public double DegToRad(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        public void HandleKeyDown(Event e)
        {
            this.currentlyPressedKeys[e.As<KeyboardEvent>().KeyCode] = true;
        }

        public void HandleKeyUp(Event e)
        {
            this.currentlyPressedKeys[e.As<KeyboardEvent>().KeyCode] = false;
        }

        public void HandleKeys()
        {
            if (currentlyPressedKeys[33])
            {
                // Page Up
                z -= 0.05;
            }

            if (currentlyPressedKeys[34])
            {
                // Page Down
                z += 0.05;
            }

            if (currentlyPressedKeys[37])
            {
                // Left cursor key
                ySpeed -= 1;
            }

            if (currentlyPressedKeys[39])
            {
                // Right cursor key
                ySpeed += 1;
            }

            if (currentlyPressedKeys[38])
            {
                // Up cursor key
                xSpeed -= 1;
            }

            if (currentlyPressedKeys[40])
            {
                // Down cursor key
                xSpeed += 1;
            }
        }

        public void InitBuffers()
        {
            this.cubeVertexPositionBuffer = gl.CreateBuffer();
            gl.BindBuffer(gl.ARRAY_BUFFER, cubeVertexPositionBuffer);

            var vertices = new double[] {
                // Front face
                -1.0, -1.0,  1.0,
                 1.0, -1.0,  1.0,
                 1.0,  1.0,  1.0,
                -1.0,  1.0,  1.0,

                // Back face
                -1.0, -1.0, -1.0,
                -1.0,  1.0, -1.0,
                 1.0,  1.0, -1.0,
                 1.0, -1.0, -1.0,

                // Top face
                -1.0,  1.0, -1.0,
                -1.0,  1.0,  1.0,
                 1.0,  1.0,  1.0,
                 1.0,  1.0, -1.0,

                // Bottom face
                -1.0, -1.0, -1.0,
                 1.0, -1.0, -1.0,
                 1.0, -1.0,  1.0,
                -1.0, -1.0,  1.0,

                // Right face
                 1.0, -1.0, -1.0,
                 1.0,  1.0, -1.0,
                 1.0,  1.0,  1.0,
                 1.0, -1.0,  1.0,

                // Left face
                -1.0, -1.0, -1.0,
                -1.0, -1.0,  1.0,
                -1.0,  1.0,  1.0,
                -1.0,  1.0, -1.0,
            };

            gl.BufferData(gl.ARRAY_BUFFER, new Float32Array(vertices), gl.STATIC_DRAW);
            this.cubeVertexNormalBuffer = gl.CreateBuffer();

            gl.BindBuffer(gl.ARRAY_BUFFER, cubeVertexNormalBuffer);

            var vertexNormals = new double[] {
                // Front face
                 0.0,  0.0,  1.0,
                 0.0,  0.0,  1.0,
                 0.0,  0.0,  1.0,
                 0.0,  0.0,  1.0,

                // Back face
                 0.0,  0.0, -1.0,
                 0.0,  0.0, -1.0,
                 0.0,  0.0, -1.0,
                 0.0,  0.0, -1.0,

                // Top face
                 0.0,  1.0,  0.0,
                 0.0,  1.0,  0.0,
                 0.0,  1.0,  0.0,
                 0.0,  1.0,  0.0,

                // Bottom face
                 0.0, -1.0,  0.0,
                 0.0, -1.0,  0.0,
                 0.0, -1.0,  0.0,
                 0.0, -1.0,  0.0,

                // Right face
                 1.0,  0.0,  0.0,
                 1.0,  0.0,  0.0,
                 1.0,  0.0,  0.0,
                 1.0,  0.0,  0.0,

                // Left face
                -1.0,  0.0,  0.0,
                -1.0,  0.0,  0.0,
                -1.0,  0.0,  0.0,
                -1.0,  0.0,  0.0
            };

            gl.BufferData(gl.ARRAY_BUFFER, new Float32Array(vertexNormals), gl.STATIC_DRAW);
            this.cubeVertexTextureCoordBuffer = gl.CreateBuffer();

            gl.BindBuffer(gl.ARRAY_BUFFER, cubeVertexTextureCoordBuffer);

            var textureCoords = new double[] {
                // Front face
                0.0, 0.0,
                1.0, 0.0,
                1.0, 1.0,
                0.0, 1.0,

                // Back face
                1.0, 0.0,
                1.0, 1.0,
                0.0, 1.0,
                0.0, 0.0,

                // Top face
                0.0, 1.0,
                0.0, 0.0,
                1.0, 0.0,
                1.0, 1.0,

                // Bottom face
                1.0, 1.0,
                0.0, 1.0,
                0.0, 0.0,
                1.0, 0.0,

                // Right face
                1.0, 0.0,
                1.0, 1.0,
                0.0, 1.0,
                0.0, 0.0,

                // Left face
                0.0, 0.0,
                1.0, 0.0,
                1.0, 1.0,
                0.0, 1.0
            };

            gl.BufferData(gl.ARRAY_BUFFER, new Float32Array(textureCoords), gl.STATIC_DRAW);

            this.cubeVertexIndexBuffer = gl.CreateBuffer();
            gl.BindBuffer(gl.ELEMENT_ARRAY_BUFFER, cubeVertexIndexBuffer);

            var cubeVertexIndices = new int[] {
                0, 1, 2,      0, 2, 3,    // Front face
                4, 5, 6,      4, 6, 7,    // Back face
                8, 9, 10,     8, 10, 11,  // Top face
                12, 13, 14,   12, 14, 15, // Bottom face
                16, 17, 18,   16, 18, 19, // Right face
                20, 21, 22,   20, 22, 23  // Left face
            };

            gl.BufferData(gl.ELEMENT_ARRAY_BUFFER, new Uint16Array(cubeVertexIndices), gl.STATIC_DRAW);
        }

        public void DrawScene()
        {
            gl.Viewport(0, 0, canvas.Width, canvas.Height);

            gl.Clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

            Script.Call("mat4.perspective", 45, (double)canvas.Width / canvas.Height, 0.1, 100.0, pMatrix);
            Script.Call("mat4.identity", mvMatrix);

            Script.Call("mat4.translate", mvMatrix, new double[] { 0.0, 0.0, z });
            Script.Call("mat4.rotate", mvMatrix, this.DegToRad(xRot), new int[] { 1, 0, 0 });
            Script.Call("mat4.rotate", mvMatrix, this.DegToRad(yRot), new int[] { 0, 1, 0 });

            gl.BindBuffer(gl.ARRAY_BUFFER, this.cubeVertexPositionBuffer);
            gl.VertexAttribPointer(this.vertexPositionAttribute, 3, gl.FLOAT, false, 0, 0);

            gl.BindBuffer(gl.ARRAY_BUFFER, this.cubeVertexNormalBuffer);
            gl.VertexAttribPointer(this.vertexNormalAttribute, 3, gl.FLOAT, false, 0, 0);

            gl.BindBuffer(gl.ARRAY_BUFFER, this.cubeVertexTextureCoordBuffer);
            gl.VertexAttribPointer(this.textureCoordAttribute, 2, gl.FLOAT, false, 0, 0);

            gl.ActiveTexture(gl.TEXTURE0);
            gl.BindTexture(gl.TEXTURE_2D, this.texture);

            gl.Uniform1i(this.samplerUniform, 0);

            // Add Blending
            gl.BlendFunc(gl.SRC_ALPHA, gl.ONE);
            gl.Enable(gl.BLEND);
            gl.Disable(gl.DEPTH_TEST);
            //gl.Uniform1f(new WebGLUniformLocation().AlphaUniform, 1.0);

            gl.BindBuffer(gl.ELEMENT_ARRAY_BUFFER, this.cubeVertexIndexBuffer);

            this.SetMatrixUniforms();

            gl.DrawElements(gl.TRIANGLES, 36, gl.UNSIGNED_SHORT, 0);
        }

        public void Animate()
        {
            var timeNow = new Date().GetTime();

            if (this.lastTime != 0)
            {
                var elapsed = timeNow - lastTime;

                xRot += (xSpeed * elapsed) / 1000.0;
                yRot += (ySpeed * elapsed) / 1000.0;
            }

            this.lastTime = timeNow;
        }

        public void Tick()
        {
            //Global.RequestAnimationFrame(Tick);

            Script.Write("requestAnimFrame(this.tick);");
            this.HandleKeys();
            this.DrawScene();
            this.Animate();
        }
    }
}
