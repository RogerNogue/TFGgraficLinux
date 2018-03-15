CC = gcc
CFLAGS = -g -Wall -Wno-deprecated
OS = $(shell uname)
LIBS = -lstdc++ -lGL -lGLU -lglut -lGLEW

sphereMarcher: main.o
	$(CC) $(CFLAGS) -o $@ $< $(LIBS)

.o:.c
	$(CC) $(CFLAGS) -c $<

clean: 
	-rm -f *.o *~ sphereMarcher

