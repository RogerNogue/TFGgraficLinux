#version 330

uniform float obsx;
uniform float obsy;
uniform float obsz;
uniform float vrpx;
uniform float vrpy;
uniform float vrpz;
uniform float upx;
uniform float upy;
uniform float upz;
uniform float fovy;
uniform float aspect;
uniform float znear;
uniform float zfar;
uniform float widthpixels;
uniform float heightpixels;

//variables per l algorisme
const int MAX_MARCHING_STEPS = 20;
const float MIN_DIST = 0.0;
const float MAX_DIST = 15.0;
const float EPSILON = 0.001;
const vec3 PAS = vec3(0,0,0.5);

out vec4 FragColor;

float magnitude(vec3 v){
	return sqrt((v.x*v.x)+(v.y*v.y)+(v.z*v.z));
}

vec3 normalize(vec3 v){
	float magnitude = magnitude(v);
	return vec3(v.x/magnitude, v.y/magnitude, v.z/magnitude);
}

vec3 crossProduct(vec3 a, vec3 b){
	return vec3(a.y*b.z - a.z*b.y, a.z*b.x - a.x*b.z, a.x*b.y - a.y*b.x);
}

vec3 getDirectionVector(vec3 obs, float h, float w, float d, vec3 vrp){
	//gl_FragCoord
	//retorno la direccio del vector que va de obs fins a vrp+d+gl_FragCoord-0.5*midapixels
	//el vec direccio de 2 punts es calcula = (x2-x1, y2-y1, z2-z1) i despres normalitzo
	//com que el vrp es (0,0,1), la d nomes la sumo a la coordenada z
	float minX, minY;//punt de la cantonada inferior esquerra
	minX = vrpx - 0.5*w;
	minY = vrpy - 0.5*h;
	float direccioX, direccioY, direccioZ;

	direccioX = minX + (w*gl_FragCoord.x)/widthpixels;
	direccioY = minY + (h*gl_FragCoord.y)/heightpixels;
	direccioZ = d;
	//normalitzo
	return normalize(vec3(direccioX, direccioY, direccioZ));
	
}

float length(vec3 p){
	return sqrt((p.x*p.x)+(p.y*p.y)+(p.z*p.z));
}

float sdSphere(vec3 p, float s )
{
	vec3 centre = vec3(0,0,3);
	return length(p-centre)-s;
}
float udBox( vec3 p )
{
  vec3 mesures = vec3(1,1,1);//meitat de les mesures
  vec3 centre = vec3(0,0,3);
  //return length(max(abs(p - centre)-mesures,0.0));
  vec3 d = abs(p - centre) - mesures;
  return min(max(d.x,max(d.y,d.z)),0.0) + length(max(d,0.0));
}

//The d1 and d2 parameters in the following functions are the distance to the two distance fields to combine together.
float opUnion( float d1, float d2 )
{
    return min(d1,d2);
}

float opSubstraction( float d1, float d2 )
{
    return max(d1,-d2);
}

float opIntersection( float d1, float d2 )
{
    return max(d1,d2);
}

float objectesEscena(vec3 punt){
	//return sdSphere(punt, 1.5);
	return udBox(punt);
	//return opUnion(sdSphere(punt, 1.5), udBox(punt)); //sembla que fa coses rares
	//return opSubstraction(udBox(punt), sdSphere(punt, 1.5));
	//return opIntersection(sdSphere(punt, 1.5), udBox(punt));
}

float rayMarching(vec3 dir){
	//versio basica inicial del algorisme
	float profunditat = MIN_DIST;
	for(int i = 0; i <= MAX_MARCHING_STEPS; ++i){
		float distColisio = objectesEscena(PAS + profunditat * dir);
		if(distColisio < EPSILON){
			return profunditat;
		}
		profunditat += distColisio;
		if(profunditat >= MAX_DIST){
			return MAX_DIST;
		}
	}
	return MAX_DIST;
}

vec3 estimacioNormal(vec3 p){
	return normalize(vec3(	
					objectesEscena(vec3(p.x + EPSILON, p.y, p.z)) - objectesEscena(vec3(p.x - EPSILON, p.y, p.z)),
					objectesEscena(vec3(p.x, p.y + EPSILON, p.z)) - objectesEscena(vec3(p.x, p.y - EPSILON, p.z)),
					objectesEscena(vec3(p.x, p.y, p.z + EPSILON)) - objectesEscena(vec3(p.x, p.y, p.z - EPSILON))
					));
}

void main()
{
	//declaracio vars
	vec3 yobs, xobs, zobs, v, aux, vrpObs, vrpVector;
	vrpVector = vec3(vrpx, vrpy, vrpz);
	vrpObs = vec3(vrpx - obsx, vrpy - obsy, vrpz - obsz);
	v = normalize(vrpObs);
	aux = crossProduct(v, vec3(upx,upy,upz));
	aux = crossProduct(aux,v);
	yobs = normalize(aux);
	zobs = -v;
	xobs = crossProduct(yobs, zobs);

	float h, w, d;
	d = magnitude(vrpObs);
	h = 2*d*tan(fovy/2);
	w = 2*d*aspect*tan(fovy/2);
	
	//pixel cantonada inferior esquerra
	vec3 minPixel = vrpVector - w/2 * xobs - h/2 * yobs;
	//<>
	//estic en un pixel de pantalla, he de saber en quin a partir de gl_FragCoord
	vec3 direction = getDirectionVector(vec3(obsx, obsy, obsz), h, w, d, vrpVector);
	//float profunditat = 1;
	float profunditat = rayMarching(direction);
	
	if(profunditat < MAX_DIST){
		FragColor = vec4(1, 0, 0, 1.0);
	}else{
		FragColor = vec4(0, 0, 0, 1.0);
	}
	
	//FragColor = vec4(direction.x, direction.y, direction.z, 1.0);
	
	
}
