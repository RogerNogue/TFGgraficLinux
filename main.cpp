#include <GL/glew.h>
#include <GL/freeglut.h>
#include <iostream>
#include <fstream>
#include <stdio.h>
#include "dataStructures/ogldev_math_3d.h"
#include "dataStructures/ogldev_util.h"


using namespace std;

GLuint VBO;

const char* pVSFileName = "shader.vs";
const char* pFSFileName = "shader.fs";
//tenir en compte el vector up si vull modificar obs i vrp.
const float oBSx = 20;
const float oBSy = 15;
const float oBSz = 25;
const float vRPx = 0;
const float vRPy = 0;
const float vRPz = 0;
const float uPx = 0;
const float uPy = 1;
const float uPz = 0;
const float fovy = 45;
const float aspect = 1.;
const float zNear = 0.1;
const float zFar = 100;
float widthPixels = 1024;
float heightPixels = 1024;

static void RenderSceneCB()
{
	glClear(GL_COLOR_BUFFER_BIT);

	glEnableVertexAttribArray(0);
	glBindBuffer(GL_ARRAY_BUFFER, VBO);
	glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, 0, 0);

	glDrawArrays(GL_TRIANGLES, 0, 6);

	glDisableVertexAttribArray(0);

	glutSwapBuffers();
}

static void CreateVertexBuffer()
{
	Vector3f Vertices[6];
	Vertices[0] = Vector3f(1.0f, 1.0f, 0.0f);
	Vertices[1] = Vector3f(-1.0f, -1.0f, 0.0f); 
	Vertices[2] = Vector3f(-1.0f, 1.0f, 0.0f);
	
	Vertices[3] = Vector3f(1.0f, 1.0f, 0.0f);
	Vertices[4] = Vector3f(1.0f, -1.0f, 0.0f);
	Vertices[5] = Vector3f(-1.0f, -1.0f, 0.0f);


	glGenBuffers(1, &VBO);
	glBindBuffer(GL_ARRAY_BUFFER, VBO);
	glBufferData(GL_ARRAY_BUFFER, sizeof(Vertices), Vertices, GL_STATIC_DRAW);
}

static void AddShader(GLuint ShaderProgram, const char* pShaderText, GLenum ShaderType)
{
	GLuint ShaderObj = glCreateShader(ShaderType);

	if (ShaderObj == 0) {
		fprintf(stderr, "Error creating shader type %d\n", ShaderType);
		exit(0);
	}

	const GLchar* p[1];
	p[0] = pShaderText;
	GLint Lengths[1];
	Lengths[0] = strlen(pShaderText);
	glShaderSource(ShaderObj, 1, p, Lengths);
	glCompileShader(ShaderObj);
	GLint success;
	glGetShaderiv(ShaderObj, GL_COMPILE_STATUS, &success);
	if (!success) {
		GLchar InfoLog[1024];
		glGetShaderInfoLog(ShaderObj, 1024, NULL, InfoLog);
		fprintf(stderr, "Error compiling shader type %d: '%s'\n", ShaderType, InfoLog);
		exit(1);
	}

	glAttachShader(ShaderProgram, ShaderObj);
}

void static cameraDeclaration() {
	gluLookAt(oBSx, oBSy, oBSz, vRPx, vRPy, vRPz, uPx, uPy, uPz);

	gluPerspective(fovy, aspect, zNear, zFar);
}

void static uniformDeclaration(GLuint ShaderProgram) {

	glUniform3f(glGetUniformLocation(ShaderProgram, "vObs"), oBSx, oBSy, oBSz);
	glUniform3f(glGetUniformLocation(ShaderProgram, "vVrp"), vRPx, vRPy, vRPz);
	glUniform3f(glGetUniformLocation(ShaderProgram, "vUp"), uPx, uPy, uPz);
	
	glUniform1f(glGetUniformLocation(ShaderProgram, "fovy"), fovy);
	glUniform1f(glGetUniformLocation(ShaderProgram, "aspect"), aspect);
	glUniform1f(glGetUniformLocation(ShaderProgram, "znear"), zNear);
	glUniform1f(glGetUniformLocation(ShaderProgram, "zfar"), zFar);
	glUniform1f(glGetUniformLocation(ShaderProgram, "widthpixels"), widthPixels);
	glUniform1f(glGetUniformLocation(ShaderProgram, "heightpixels"), heightPixels);
}

static void CompileShaders()
{
	GLuint ShaderProgram = glCreateProgram();

	if (ShaderProgram == 0) {
		fprintf(stderr, "Error creating shader program\n");
		exit(1);
	}

	string vs, fs;

	if (!ReadFile(pVSFileName, vs)) {
		exit(1);
	};

	if (!ReadFile(pFSFileName, fs)) {
		exit(1);
	};

	AddShader(ShaderProgram, vs.c_str(), GL_VERTEX_SHADER);
	AddShader(ShaderProgram, fs.c_str(), GL_FRAGMENT_SHADER);

	GLint Success = 0;
	GLchar ErrorLog[1024] = { 0 };

	glLinkProgram(ShaderProgram);
	glGetProgramiv(ShaderProgram, GL_LINK_STATUS, &Success);
	if (Success == 0) {
		glGetProgramInfoLog(ShaderProgram, sizeof(ErrorLog), NULL, ErrorLog);
		fprintf(stderr, "Error linking shader program: '%s'\n", ErrorLog);
		exit(1);
	}

	glValidateProgram(ShaderProgram);
	glGetProgramiv(ShaderProgram, GL_VALIDATE_STATUS, &Success);
	if (!Success) {
		glGetProgramInfoLog(ShaderProgram, sizeof(ErrorLog), NULL, ErrorLog);
		fprintf(stderr, "Invalid shader program: '%s'\n", ErrorLog);
		exit(1);
	}

	glUseProgram(ShaderProgram);
	uniformDeclaration(ShaderProgram);
}

void processKeys(unsigned char key, int x, int y) {
	switch (key) {
	case 27: //glutDestroyWindow(ShaderProgram);
		exit(0);
		break;
	case 'r':
		CompileShaders();
		glutPostRedisplay();
		break;
	}
}

void updateWindowValues(int width, int height) {
	widthPixels = float(width);
	heightPixels = float(height);
	glViewport(0, 0, width, height);
	CompileShaders();
}

static void InitializeGlutCallbacks()
{
	glutDisplayFunc(RenderSceneCB);
	glutKeyboardFunc(processKeys);
	glutReshapeFunc(updateWindowValues);
}

int main(int argc, char** argv)
{
	glutInit(&argc, argv);
	glutInitDisplayMode(GLUT_DOUBLE | GLUT_RGB);
	glutInitWindowSize(widthPixels, heightPixels);
	glutInitWindowPosition(500, 0);
	glutCreateWindow("Visualizer");

	cameraDeclaration();

	InitializeGlutCallbacks();

	// Must be done after glut is initialized!
	GLenum res = glewInit();
	if (res != GLEW_OK) {
		fprintf(stderr, "Error: '%s'\n", glewGetErrorString(res));
		return 1;
	}

	printf("GL version: %s\n", glGetString(GL_VERSION));

	glClearColor(0.0f, 0.0f, 0.0f, 0.0f);

	CreateVertexBuffer();

	CompileShaders();

	glutMainLoop();

	return 0;
}
