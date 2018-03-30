#version 330

//per algun motiu nomes executa si tinc aquestes 2 vars declarades


uniform vec3 vObs;
uniform vec3 vVrp;
uniform vec3 vUp;

uniform float fovy;
uniform float aspect;
uniform float znear;
uniform float zfar;
uniform float widthpixels;
uniform float heightpixels;

//variables per l algorisme
const int MAX_MARCHING_STEPS = 100;
const float MIN_DIST = 0.0;
const float MAX_DIST = 25.0;
const float EPSILON = 0.001;

//cada vec es 1 columna
mat4 identityTransf =	mat4(vec4(1, 0, 0, 0),vec4(0, 1, 0, 0),vec4(0, 0, 1, 0),vec4(1, 1, -1.5, 1));

vec3 llumPuntual = vec3(0,-5,0);

out vec4 FragColor;

vec3 crossProduct(vec3 a, vec3 b){
	return vec3(a.y*b.z - a.z*b.y, a.z*b.x - a.x*b.z, a.x*b.y - a.y*b.x);
}

vec3 getDirectionVectorNew(vec3 obs, float h, float w, float d, vec3 vrp, vec3 xobs, vec3 yobs){
	vec3 min = vrp - w/2 * xobs - h/2 * yobs;

	vec3 direccio  = min + (w*gl_FragCoord.x)/widthpixels*xobs + (h*gl_FragCoord.y)/heightpixels * yobs;// + (w/2*widthpixels, h/2*heightpixels);
	return normalize(direccio-obs);
	//return min;
}

float sdSphere(vec3 p, float s, mat4 transfMatrix )
{
	p = (inverse(transfMatrix)* vec4(p, 1.0)).xyz;
	//vec3 centre = vec3(1,1,0);
	return length(p)-s;
}

float udBox( vec3 p, mat4 transfMatrix)
{
  p = (inverse(transfMatrix)* vec4(p, 1.0)).xyz;
  vec3 mesures = vec3(1,1,1);//meitat de les mesures
  vec3 d = abs(p) - mesures;
  return min(max(d.x,max(d.y,d.z)),0.0) + length(max(d,0.0));
}

float sdTorus( vec3 p, mat4 transfMatrix)
{
  p = (inverse(transfMatrix)* vec4(p, 1.0)).xyz;
  vec2 t = vec2(1.0,0.5);
  vec2 q = vec2(length(p.xz)-t.x,p.y);
  return length(q)-t.y;
}

float sdCylinder( vec3 p, mat4 transfMatrix)
{
  p = (inverse(transfMatrix)* vec4(p, 1.0)).xyz;
  vec3 c = vec3(1.0,0.0,0.5);
  return length(p.xz-c.xy)-c.z;
}

float sdCappedCylinder( vec3 p, vec2 h, mat4 transfMatrix)
{
  p = (inverse(transfMatrix)* vec4(p, 1.0)).xyz;
  vec2 d = abs(vec2(length(p.xz),p.y)) - h;
  return min(max(d.x,d.y),0.0) + length(max(d,0.0));
}

float sdCone( vec3 p, mat4 transfMatrix)
{
  p = (inverse(transfMatrix)* vec4(p, 1.0)).xyz;
	//es com que el con es molt gran
	// c must be normalized
	vec2 c = normalize(vec2(0.5,0.5));
    float q = length(p.xy);
    return dot(c,vec2(q,p.z));
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

mat4 translation(float x, float y, float z){
	return mat4(vec4(1, 0, 0, 0),vec4(0, 1, 0, 0),vec4(0, 0, 1, 0),vec4(x, y, z, 1));
}

mat4 rotation(int axis, float angle){
	//axis == 0 x, axis == 1 y, else z
	if(axis == 0){
		return mat4(vec4(1, 0, 0, 0),vec4(0, cos(angle), sin(angle), 0),vec4(0, -sin(angle), cos(angle), 0),vec4(0, 0, 0, 1));
	}else if(axis == 1){
		return mat4(vec4(cos(angle), 0, sin(angle), 0),vec4(0, 1, 0, 0),vec4(-sin(angle), 0, cos(angle), 0),vec4(0, 0, 0, 1));
	}else{
		return mat4(vec4(cos(angle), sin(angle), 0, 0),vec4(-sin(angle), cos(angle), 0, 0),vec4(0, 0, 1, 0),vec4(0, 0, 0, 1));
	}
	
}

float objectesEscena(vec3 punt){
//les trans es fan en ordre de multiplicacio i les rotacions sobre l objecte, no sobre l origen. 
//Un cop l objecte esta rotat, tambe ho estan els seues eixos

	return sdSphere(punt, 1, translation(0,0,0));
	//return udBox(punt, translation(2,0,0)*rotation(0, 180)*translation(0, 2, 0));
	//return udBox(punt, translation(2,0,0)*translation(0, 2, 0)*rotation(0, -180));
	//return sdTorus(punt, rotation(0, 90));
	//return sdCylinder(punt, rotation(0, 90));
	//return sdCappedCylinder(punt, vec2(1, 1), rotation(0, 0));
	//return sdCone(punt, translation(0,0,-2)*rotation(0,5));
	//return opUnion(sdSphere(punt, 1.5), udBox(punt)); //sembla que fa coses rares
	//return opSubstraction(udBox(punt), sdSphere(punt, 1.5));
	//return opIntersection(sdSphere(punt, 1.5), udBox(punt));
}

float rayMarching(vec3 obs, vec3 dir){
	//versio basica inicial del algorisme
	float profunditat = MIN_DIST;
	for(int i = 0; i <= MAX_MARCHING_STEPS; ++i){
		float distColisio = objectesEscena(obs + profunditat * dir);
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
	vec3 yobs, xobs, zobs, v, aux, vrpObs;
	vrpObs = vVrp - vObs;
	v = normalize(vrpObs);
	aux = crossProduct(v,vUp);
	aux = crossProduct(aux,v);
	yobs = normalize(aux);
	zobs = -v;
	xobs = crossProduct(yobs, zobs);

	float h, w, d;
	d = length(vrpObs);
	h = 2*d*tan(fovy/2);
	w = 2*d*aspect*tan(fovy/2);
	
	vec3 direction = getDirectionVectorNew(vObs, h, w, d, vVrp, xobs, yobs);
	float profunditat = rayMarching(vObs, direction);

	if(profunditat < MAX_DIST){
		vec3 puntcolisio = vObs + profunditat * direction;
		vec3 normal = estimacioNormal(puntcolisio);
		//vec3 color = vec3(1,1,1) * normal.z;//llum al origen
		vec3 color = vec3(1,1,1) * dot(normal, (llumPuntual - puntcolisio)); //llum a la posicio de llumPuntual
		FragColor = vec4(color, 1.0);
	}else{
		FragColor = vec4(0.9, 0.2, 0.2, 1.0);
	}
	//FragColor = vec4(1,1,0, 1.0);
	
}
