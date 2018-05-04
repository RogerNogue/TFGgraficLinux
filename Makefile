CC = gcc
CFLAGS = -O2 -Wall -Wno-deprecated
OS = $(shell uname)
LIBS = -lstdc++ -lGL -lGLU -lglut -lGLEW

OBJS = main.o dataStructures/ogldev_util.o

sphereMarcher: $(OBJS)
	$(CC) $(CFLAGS) -o $@ $^ $(LIBS)

.o:.c
	$(CC) $(CFLAGS) -c $<

ogldev_util.o:dataStructures/ogldev_util.cpp
	$(CC) $(CFLAGS) -c $<

clean: 
	-rm -f *.o *~ sphereMarcher

