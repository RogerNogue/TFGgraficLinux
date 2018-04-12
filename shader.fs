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
const float MAX_LIGHT_DIST = 25.0;
const int MAX_MARCHING_LIGHT_STEPS = 100;
const float EPSILON = 0.001;
int lightReached = 0;

//cada vec es 1 columna
mat4 identityTransf =	mat4(vec4(1, 0, 0, 0),vec4(0, 1, 0, 0),vec4(0, 0, 1, 0),vec4(1, 1, -1.5, 1));

vec3 llumPuntual = vec3(5,5,0);

out vec4 FragColor;
/*
materials:
1 -> emerald
2 -> jade
3 -> ruby
4 -> red plastic
5 -> cyan plastic
6 -> green plastic
7 -> red rubber
8 -> cyan rubber
9 -> green rubber
10 -> pearl
*/

vec3 materialAmbient[10] = vec3[10](
	vec3(0.0215, 0.1745, 0.0215),
	vec3(0.135, 0.2225, 0.1575),
	vec3(0.1745, 0.01175, 0.01175),
	vec3(0.0, 0.0, 0.0),
	vec3(0, 0.1, 0.06),
	vec3(0, 0, 0),
	vec3(0.05, 0, 0),
	vec3(0, 0.05, 0.05),
	vec3(0, 0.05, 0),
	vec3(0.25, 0.20725, 0.20725)
	);

vec3 materialDiffuse[10] = vec3[10](
	vec3(0.07568, 0.61424, 0.07568),
	vec3(0.54, 0.89, 0.63),
	vec3(0.61424, 0.04136, 0.04136),
	vec3(0.5, 0, 0),
	vec3(0, 0.51, 0.51),
	vec3(0.1, 0.35, 0.1),
	vec3(0.5, 0.4, 0.4),
	vec3(0.4, 0.5, 0.5),
	vec3(0.4, 0.5, 0.4),
	vec3(1, 0.829, 0.829)
	);
vec3 ambientColor(int material){
	if(material < 0 || material > 10) return vec3(1,1,1);
	return materialAmbient[material-1];
}

vec3 diffuseColor(int material){
	if(material < 0 || material > 10) return vec3(1,1,1);
	return materialDiffuse[material-1];
}

vec3 crossProduct(vec3 a, vec3 b){
	return vec3(a.y*b.z - a.z*b.y, a.z*b.x - a.x*b.z, a.x*b.y - a.y*b.x);
}

vec3 getDirectionVectorNew(vec3 obs, float h, float w, float d, vec3 vrp, vec3 xobs, vec3 yobs){
	vec3 min = vrp - w/2 * xobs - h/2 * yobs;

	vec3 direccio  = min + (w*gl_FragCoord.x)/widthpixels*xobs + (h*gl_FragCoord.y)/heightpixels * yobs;// + (w/2*widthpixels, h/2*heightpixels);
	return normalize(direccio-obs);
	//return min;
}

vec2 sdSphere(vec3 p, float s, mat4 transfMatrix, float material)
{
	p = (inverse(transfMatrix)* vec4(p, 1.0)).xyz;
	//vec3 centre = vec3(1,1,0);
	return vec2(length(p)-s, material);
}

vec2 udBox( vec3 p, mat4 transfMatrix, vec3 mesures, float material)
{
  p = (inverse(transfMatrix)* vec4(p, 1.0)).xyz;
  //vec3 mesures = vec3(1,1,1);//meitat de les mesures
  vec3 d = abs(p) - mesures;
  return vec2(min(max(d.x,max(d.y,d.z)),0.0) + length(max(d,0.0)), material);
}

vec2 sdTorus( vec3 p, mat4 transfMatrix, float material)
{
  p = (inverse(transfMatrix)* vec4(p, 1.0)).xyz;
  vec2 t = vec2(1.0,0.5);
  vec2 q = vec2(length(p.xz)-t.x,p.y);
  return vec2(length(q)-t.y, material);
}

vec2 sdCylinder( vec3 p, mat4 transfMatrix, float material)
{
  p = (inverse(transfMatrix)* vec4(p, 1.0)).xyz;
  vec3 c = vec3(1.0,0.0,0.5);
  return vec2(length(p.xz-c.xy)-c.z, material);
}

vec2 sdCappedCylinder( vec3 p, vec2 h, mat4 transfMatrix, float material)
{
  p = (inverse(transfMatrix)* vec4(p, 1.0)).xyz;
  vec2 d = abs(vec2(length(p.xz),p.y)) - h;
  return vec2(min(max(d.x,d.y),0.0) + length(max(d,0.0)), material);
}

vec2 sdCone( vec3 p, mat4 transfMatrix, float material)
{
  p = (inverse(transfMatrix)* vec4(p, 1.0)).xyz;
	//es com que el con es molt gran
	// c must be normalized
	vec2 c = normalize(vec2(2,1.5));
    float q = length(p.xy);
    return vec2(dot(c,vec2(q,p.z)), material);
}

vec2 sdPlane( vec3 p, vec4 n, mat4 transfMatrix, float material )
{
  // n must be normalized
  p = (inverse(transfMatrix)* vec4(p, 1.0)).xyz;
  return vec2(dot(p,n.xyz) + n.w, material);
}

//The d1 and d2 parameters in the following functions are the distance to the two distance fields to combine together.
vec2 opUnion( vec2 d1, vec2 d2 )
{
    //return min(d1,d2); //<>
	if(d1.x < d2.x) return d1;
	return d2;
}

vec2 opSubstraction( vec2 d1, vec2 d2 )
{
    //return max(d1,-d2);
	if(d1.x > -d2.x) return d1;
	return -d2;
}

vec2 opIntersection( vec2 d1, vec2 d2 )
{
    //return max(d1,d2);
	if(d1.x > d2.x) return d1;
	return d2;
}

mat4 translation(float x, float y, float z){
	return mat4(vec4(1, 0, 0, 0),vec4(0, 1, 0, 0),vec4(0, 0, 1, 0),vec4(x, y, z, 1));
}

mat4 rotation(int axis, float angle){
	//axis == 0 x, axis == 1 y, else z
	angle = radians(angle);
	if(axis == 0){
		return mat4(vec4(1, 0, 0, 0),vec4(0, cos(angle), sin(angle), 0),vec4(0, -sin(angle), cos(angle), 0),vec4(0, 0, 0, 1));
	}else if(axis == 1){
		return mat4(vec4(cos(angle), 0, sin(angle), 0),vec4(0, 1, 0, 0),vec4(-sin(angle), 0, cos(angle), 0),vec4(0, 0, 0, 1));
	}else{
		return mat4(vec4(cos(angle), sin(angle), 0, 0),vec4(-sin(angle), cos(angle), 0, 0),vec4(0, 0, 1, 0),vec4(0, 0, 0, 1));
	}
	
}


vec2 objectesEscena(vec3 punt){
//les trans es fan en ordre de multiplicacio i les rotacions sobre l objecte, no sobre l origen. 
//Un cop l objecte esta rotat, tambe ho estan els seues eixos

	//return sdSphere(punt, 1, translation(0,0,0), 10.);
	//return udBox(punt, translation(2,0,-1), vec3(1,1,1), 10.);
	//return udBox(punt, translation(2,0,0)*translation(0, 2, 0)*rotation(0, -180), 10.);
	//return sdTorus(punt, rotation(0, 90), 10);
	//return sdCylinder(punt, rotation(0, 90), 10);
	//return sdCappedCylinder(punt, vec2(1, 1), rotation(0, 0), 10);
	//return sdCone(punt, translation(0,0,-2)*rotation(0,90), 10);
	//return sdPlane(punt, normalize(vec4(1, 1, 1, 1)), translation(0,0,0), 10.);
	//return opUnion(sdSphere(punt, 1, translation(0,0,0), 10.), udBox(punt, translation(2,0,-1), 10.));
	//return opSubstraction(udBox(punt, translation(0,0,0), 10.), sdSphere(punt, 1.5, translation(0,0,0), 10.));
	//return opIntersection(sdSphere(punt, 1.5, translation(0,0,0), 10.), udBox(punt, translation(0,0,0), 10.));

	//escena test
	//return opUnion(opUnion(sdSphere(punt, 1, translation(0,2,-1), 2.), udBox(punt, translation(0,0,-1), vec3(1,1,1), 6.)), udBox(punt, translation(0,-1,0), vec3(5, 0.01, 5), 10));

	//escena amb totes les figures
	return 
			opUnion(
			opUnion(
			opUnion(
			opUnion(
			opUnion(
			opUnion( opUnion(sdSphere(punt, 1, translation(3,0,-1), 2.), udBox(punt, translation(0,0,-1), vec3(1,1,1), 6.)), udBox(punt, translation(0,-1,0), vec3(10, 0.01, 10), 10)), sdTorus(punt, translation(-3,-1,0), 1)), sdCylinder(punt, translation(2,-1, -4), 3))
						,sdCappedCylinder(punt, vec2(1, 1), translation(-3,0,-3), 4)), sdCone(punt, translation(0,0,-5)*rotation(0,90), 5)), opSubstraction(udBox(punt, translation(5,0,-4), vec3(1, 1, 1), 6), sdSphere(punt, 1.5, translation(5,0,-4), 6))) ;
	
	//return min( sdSphere(punt, 1, translation(-2,1,-2)), udBox(punt, translation(2,1,-2))); //amb els canvis dels materials no funciona
	
}

vec3 estimacioNormal(vec3 p){
	return normalize(vec3(	
					objectesEscena(vec3(p.x + EPSILON, p.y, p.z)).x - objectesEscena(vec3(p.x - EPSILON, p.y, p.z)).x,
					objectesEscena(vec3(p.x, p.y + EPSILON, p.z)).x - objectesEscena(vec3(p.x, p.y - EPSILON, p.z)).x,
					objectesEscena(vec3(p.x, p.y, p.z + EPSILON)).x - objectesEscena(vec3(p.x, p.y, p.z - EPSILON)).x
					));
}

void lightMarching(vec3 obs, float profunditat, vec3 dir){
	//straight to the light source
	float profCercaLlum =2*EPSILON; //quan el valor es baix, sembla que xoca amb el mateix objecte
	vec3 puntcolisio = vObs + profunditat * dir;
	puntcolisio = puntcolisio + 2*EPSILON*estimacioNormal(puntcolisio);
	vec3 direccioLlum = normalize(llumPuntual - puntcolisio);
	for(int i = 0; i <= MAX_MARCHING_LIGHT_STEPS; ++i){
		vec3 puntActual = puntcolisio + profCercaLlum * direccioLlum;
		float distColisio = objectesEscena(puntActual).x;
		float distLlum = length(llumPuntual - (puntActual));

		if(distColisio < EPSILON){
			return;
		}
		
		if(distLlum < distColisio){
			lightReached = 1;
		}
		
		profCercaLlum += distColisio;
		
	}
}

vec2 rayMarching(vec3 obs, vec3 dir){
	//versio basica inicial del algorisme
	
	float profunditat = MIN_DIST;
	int material;
	for(int i = 0; i <= MAX_MARCHING_STEPS; ++i){
		vec2 colisio = objectesEscena(obs + profunditat * dir);

		float distColisio = colisio.x;
		material = int(colisio.y);
		if(distColisio < EPSILON){
			lightMarching(obs, profunditat, dir );
			return vec2(profunditat, material);
		}
		profunditat += distColisio;
		if(profunditat >= MAX_DIST){
			return vec2(MAX_DIST, 0.);
		}
	}
	
	return vec2(MAX_DIST, 0.);
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
	vec2 prof = rayMarching(vObs, direction);
	float profunditat = prof.x;
	float material = prof.y;

	if(profunditat < MAX_DIST){
		vec3 puntcolisio = vObs + profunditat * direction;
		vec3 normal = estimacioNormal(puntcolisio);
		vec3 color = vec3(0,0,0) * normal.z;//llum al origen
		vec3 colAmbient = ambientColor(int(material));
		if(lightReached == 1){
			color = diffuseColor(int(material)) * (lightReached ) * dot(normal, (llumPuntual - puntcolisio)); //llum a la posicio de llumPuntual
			color.x = max(colAmbient.x, color.x);
			color.y = max(colAmbient.y, color.y);
			color.z = max(colAmbient.z, color.z);
			//color = diffuseColor(int(material));
		}else{
			color = colAmbient;
		}
		
		FragColor = vec4(color, 1.0);
	}else{
		FragColor = vec4(0.9, 0.2, 0.2, 1.0);
	}
	//FragColor = vec4(1,1,0, 1.0);
	
}
