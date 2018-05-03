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
const float MAX_REFLECTION_STEPS = 50;
const int MAX_MARCHING_STEPS = 100;
const float MIN_DIST = 0.0;
const float MAX_DIST = 75.0;
const float MAX_LIGHT_DIST = 25.0;
const int MAX_MARCHING_LIGHT_STEPS = 125;
const float EPSILON = 0.001;
//int lightReached = 0;

//cada vec es 1 columna
mat4 identityTransf =	mat4(vec4(1, 0, 0, 0),vec4(0, 1, 0, 0),vec4(0, 0, 1, 0),vec4(0, 0, 0, 1));

//posicio, intensitat
vec4 llumsPuntuals[3] = vec4[3](
	vec4(-10, 2, 5, 0.4),
	vec4(-5, 1, -15, 0.3),
	vec4(10,10,5, 0.4)
	);

	//vec4(10,20,5, 0.9)

int lightsReached[3] = int[3](
	0,0,0
	);

float specularIntensity[3] = float[3](
	0,0,0
	);

float dmin[3] = float[3](
	MAX_DIST, MAX_DIST, MAX_DIST
	);

out vec4 FragColor;

float relaxationIndex = 1.9;//pertany a [1, 2)

float obscurancia = 0;
float epsilonOcclusion = 0.5;

/*
http://devernay.free.fr/cours/opengl/materials.html
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
11 -> yellow
*/

vec3 materialAmbient[11] = vec3[11](
	vec3(0.0215, 0.1745, 0.0215),
	vec3(0.135, 0.2225, 0.1575),
	vec3(0.1745, 0.01175, 0.01175),
	vec3(0.0, 0.0, 0.0),
	vec3(0, 0.1, 0.06),
	vec3(0, 0, 0),
	vec3(0.05, 0, 0),
	vec3(0, 0.05, 0.05),
	vec3(0, 0.05, 0),
	vec3(0.25, 0.20725, 0.20725),
	vec3(0.4, 0.32, 0.2)
	);

vec3 materialDiffuse[11] = vec3[11](
	vec3(0.07568, 0.61424, 0.07568),
	vec3(0.54, 0.89, 0.63),
	vec3(0.61424, 0.04136, 0.04136),
	vec3(0.5, 0, 0),
	vec3(0, 0.51, 0.51),
	vec3(0.1, 0.35, 0.1),
	vec3(0.5, 0.4, 0.4),
	vec3(0.4, 0.5, 0.5),
	vec3(0.4, 0.5, 0.4),
	vec3(1, 0.829, 0.829),
	vec3(0.7, 0.62, 0.4)
	);
//utilitzare el shininess com a coeficient de reflexio
//vec4, specular, shininess
vec4 materialSpecular[11] = vec4[11](
	vec4(0.633, 0.727811, 0.633, 0.6),
	vec4(0.316, 0.316, 0.316, 0.1),
	vec4(0.728, 0.627, 0.627, 0.6),
	vec4(0.7, 0.6, 0.6, 0.25),
	vec4(0.5, 0.5, 0.5, 0.25),
	vec4(0.45, 0.55, 0.45, 0.25),
	vec4(0.7, 0.04, 0.04, 0.078),
	vec4(0.04, 0.7, 0.7, 0.078),
	vec4(0.04, 0.7, 0.04, 0.078),
	vec4(0.29, 0.29, 0.29, 0.088),
	vec4(0.8, 0.8, 0.5, 0.078)
	);
vec3 ambientColor(int material){
	if(material < 0 || material > 11) return vec3(1,1,1);
	return materialAmbient[material-1];
}

vec3 diffuseColor(int material){
	if(material < 0 || material > 11) return vec3(1,1,1);
	return materialDiffuse[material-1];
}

vec4 specularColor(int material){
	if(material < 0 || material > 11) return vec4(1,1,1, 1);
	return materialSpecular[material-1];
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
	return vec2(-d2.x, d2.y);
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
//funcio on es declara l escena
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
			opUnion( opUnion(sdSphere(punt, 1, translation(5,0,0), 2.), udBox(punt, translation(0,0,0), vec3(1,1,1), 8.)), udBox(punt, translation(0,-1,-5), vec3(10, 0.01, 10), 10)), sdTorus(punt, translation(-5,-0.5,0), 1)),  opIntersection(sdCylinder(punt, translation(4.5,-1, -8), 3), udBox(punt, translation(5,0,-8),vec3(2,2,2), 3)))
						,sdCappedCylinder(punt, vec2(1, 1), translation(-5,0,-4), 4)), opIntersection(sdCone(punt, translation(0,0,-5)*rotation(0,90), 5), udBox(punt, translation(0,0,-5),vec3(2,2,2), 5.))), opSubstraction(udBox(punt, translation(5,0,-4), vec3(1, 1, 1), 6), sdSphere(punt, 1.5, translation(5,0,-4), 6))) ;
	
	/*
	//floor
	vec2 floor = udBox(punt, translation(0,0,-5), vec3(30, 0.01, 30), 10);

	//bloc edifici 1
	vec2 estructuraEdifici1 = udBox(punt, translation(0,4,0), vec3(3,8,3), 10.);

	//decoracio
	vec2 decoracioEdifici11 = opUnion( opUnion(udBox(punt, translation(3,4,1), vec3(0.01,0.5,0.3), 2.), udBox(punt, translation(3,7,1), vec3(0.01,0.5,0.3), 2.)), udBox(punt, translation(3,0.5,0), vec3(0.01,0.5,0.3), 4.));

	vec2 decoracioEdifici12 = opUnion( opUnion(udBox(punt, translation(3,4,-1), vec3(0.01,0.5,0.3), 2.), udBox(punt, translation(3,10,-1), vec3(0.01,0.5,0.3), 2.)), udBox(punt, translation(3,10,1), vec3(0.01,0.5,0.3), 2.));

	vec2 decoracioEdifici13 = udBox(punt, translation(3,7,-1), vec3(0.01,0.5,0.3), 2.);

	//unio parts edifici 1

	vec2 edifici1 = opUnion(opUnion(opUnion(estructuraEdifici1, decoracioEdifici11), decoracioEdifici12), decoracioEdifici13);

	//bloc edifici 2
	vec2 estructuraEdifici2 = udBox(punt, translation(0,4,10), vec3(3,8,3), 10.);

	//decoracio
	vec2 decoracioEdifici21 = opUnion( opUnion(udBox(punt, translation(3,7,11), vec3(0.01,0.5,0.3), 2.), udBox(punt, translation(3,4,9), vec3(0.01,0.5,0.3), 2.)), udBox(punt, translation(3,4,11), vec3(0.01,0.5,0.3), 2.));

	vec2 decoracioEdifici22 = opUnion( opUnion(udBox(punt, translation(3,10,9), vec3(0.01,0.5,0.3), 2.), udBox(punt, translation(3,10,11), vec3(0.01,0.5,0.3), 2.)), udBox(punt, translation(3,7,9), vec3(0.01,0.5,0.3), 2.));

	vec2 decoracioEdifici23 = udBox(punt, translation(3,0.5,9.5), vec3(0.01,0.5,0.3), 4.);

	//unio parts edifici 2
	vec2 edifici2 = opUnion(opUnion(opUnion(estructuraEdifici2, decoracioEdifici21), decoracioEdifici22), decoracioEdifici23);

	//bloc edifici 3
	vec2 estructuraedifici3 = udBox(punt, translation(0,4,20), vec3(3,8,3), 10.);

	//decoracio
	vec2 decoracioEdifici31 = opUnion(opUnion(udBox(punt, translation(3,7,19), vec3(0.01,0.5,0.3), 2.), udBox(punt, translation(3,4,19), vec3(0.01,0.5,0.3), 2.)), udBox(punt, translation(3,0.5,20), vec3(0.01,0.5,0.3), 4.));

	//unio parts edifici 3
	vec2 edifici3 = opUnion(estructuraedifici3, decoracioEdifici31);

	//bloc edifici 4
	vec2 edifici4 = udBox(punt, translation(0,4,-10), vec3(3,8,3), 10.);

	//bloc edifici 5
	vec2 estructuraEdifici5 =  udBox(punt, translation(10,4,0), vec3(3,8,3), 10.);

	//decoracio
	vec2 decoracioEdifici51 = opUnion(opUnion(udBox(punt, translation(9,10,3), vec3(0.3,0.5,0.01), 2.), udBox(punt, translation(11,4,3), vec3(0.3,0.5,0.01), 2.)), udBox(punt, translation(9,7,3), vec3(0.3,0.5,0.01), 2.));

	vec2 decoracioEdifici52 = opUnion(opUnion(udBox(punt, translation(9.8,0.5,3), vec3(0.3,0.5,0.01), 4.), udBox(punt, translation(11,10,3), vec3(0.3,0.5,0.01), 2.)),  udBox(punt, translation(11,7,3), vec3(0.3,0.5,0.01), 2.));

	vec2 decoracioEdifici53 = udBox(punt, translation(9,4,3), vec3(0.3,0.5,0.01), 2.);

	//unio parts edifici 5
	vec2 edifici5 = opUnion(opUnion(opUnion(estructuraEdifici5, decoracioEdifici51), decoracioEdifici52), decoracioEdifici53);

	//unio edificis sencers
	vec2 edificis = opUnion(opUnion(opUnion(opUnion(edifici5, edifici4), edifici3), edifici2), edifici1);

	//pati i continguts
	vec2 pati = udBox(punt, translation(15,0.01,15), vec3(10,0.01,10), 1.);

	vec2 camp = opUnion(udBox(punt, translation(12,0.015,12), vec3(4,0.01,5.5), 3.), udBox(punt, translation(12,0.02,12), vec3(3.5,0.01,5), 5.));

	vec2 valles = opUnion(udBox(punt, translation(15,0.,5), vec3(10,1,0.25), 3.), udBox(punt, translation(5,0.,17), vec3(0.25,1,10), 3.));

	vec2 portes = opUnion(sdCappedCylinder(punt, vec2(0.3, 1.5), translation(5,0,7), 7), sdCappedCylinder(punt, vec2(0.3, 1.5), translation(5,0,5), 7));
	//unio estructura pati

	vec2 unioPati = opUnion(opUnion(opUnion(portes, valles), camp), pati);

	//porteria1
	vec2 porteria11 = opUnion(opUnion(udBox(punt, translation(11,0,8), vec3(0.05,1,0.02), 10.), udBox(punt, translation(13,0,8), vec3(0.05,1,0.02), 10.)), udBox(punt, translation(12,1,8), vec3(1.05,0.05,0.02), 10.));
	
	vec2 porteria12 = opUnion(opUnion(sdCappedCylinder(punt, vec2(0.05, 1.05), translation(11,0.15,7.3)*rotation(0,42), 10), udBox(punt, translation(10.95,0.1,7.6), vec3(0.1,0.1,0.4), 10.)), udBox(punt, translation(13.05,0.1,7.6), vec3(0.1,0.1,0.4), 10.));

	vec2 porteria13 =  sdCappedCylinder(punt, vec2(0.05, 1.05), translation(13.05,0.2,7.3)*rotation(0,42), 10);

	//unio porteria1

	vec2 porteria1 = opUnion(opUnion(porteria11, porteria12), porteria13);

	//porteria 2
	vec2 porteria21 = opUnion(opUnion(udBox(punt, translation(13,0,16.25), vec3(0.05,1,0.02), 10.), udBox(punt, translation(11,0,16.25), vec3(0.05,1,0.02), 10.)),udBox(punt, translation(12,1,16.25), vec3(1.05,0.05,0.02), 10.));

	vec2 porteria22 = opUnion(opUnion(sdCappedCylinder(punt, vec2(0.05, 1.05), translation(11,0.15,16.9)*rotation(0,-40), 10), udBox(punt, translation(10.95,0.1,16.6), vec3(0.1,0.1,0.4), 10.)), udBox(punt, translation(13.05,0.1,16.6), vec3(0.1,0.1,0.4), 10.));

	vec2 porteria23 = sdCappedCylinder(punt, vec2(0.05, 1.00), translation(13.05,0.2,16.9)*rotation(0,-40), 10);

	//unio porteria2
	vec2 porteria2 = opUnion(opUnion(porteria21, porteria22), porteria23);

	//pilotes
	vec2 pilotes1 = opUnion(opUnion(sdSphere(punt, 0.1, translation(10,0.1,18.0), 11.), sdSphere(punt, 0.1, translation(12,0.1,15), 11.)), sdSphere(punt, 0.1, translation(13,0.1,16.0), 11.));

	vec2 pilotes2 = opUnion(sdSphere(punt, 0.1, translation(9,0.1,16.4), 11.), sdSphere(punt, 0.1, translation(11.2,0.1,16.6), 11.));

	vec2 pilotes = opUnion(pilotes1, pilotes2);

	//unio pati sencer

	vec2 patiSencer = opUnion(opUnion(opUnion(unioPati, pilotes), porteria2), porteria1);

	//unio escena
	vec2 escena = opUnion(opUnion(patiSencer, edificis), floor);

	//retorn del resultat
	return escena;
	*/
	//return min( sdSphere(punt, 1, translation(-2,1,-2)), udBox(punt, translation(2,1,-2))); //amb els canvis dels materials no funciona
	
}

vec3 estimacioNormal(vec3 p){
//estimacio de la normal en un punt que es troba molt a prop de la superficie d un objecte
	return normalize(vec3(	
					objectesEscena(vec3(p.x + EPSILON, p.y, p.z)).x - objectesEscena(vec3(p.x - EPSILON, p.y, p.z)).x,
					objectesEscena(vec3(p.x, p.y + EPSILON, p.z)).x - objectesEscena(vec3(p.x, p.y - EPSILON, p.z)).x,
					objectesEscena(vec3(p.x, p.y, p.z + EPSILON)).x - objectesEscena(vec3(p.x, p.y, p.z - EPSILON)).x
					));
}

void lightMarching(vec3 obs, vec3 puntcolisio){
	//funcio que va des del punt de colisio del algorisme cap a la llum
	puntcolisio += 5*EPSILON*estimacioNormal(puntcolisio);
	//puntcolisio = puntcolisio + 0.5*estimacioNormal(puntcolisio);
	for(int j = 0; j < llumsPuntuals.length(); ++j){
		float profCercaLlum =2*EPSILON; //quan el valor es baix, sembla que xoca amb el mateix objecte
		vec3 direccioLlum = normalize(llumsPuntuals[j].xyz - puntcolisio);
		for(int i = 0; i <= MAX_MARCHING_LIGHT_STEPS; ++i){
			vec3 puntActual = puntcolisio + profCercaLlum * direccioLlum;
			float distColisio = objectesEscena(puntActual).x;
			float distLlum = length(llumsPuntuals[j].xyz - (puntActual));

			//calcul distancia mes propera per calcular les ombres suaus mes endavant
			if(profCercaLlum > 0.4 && distColisio < dmin[j])	dmin[j] = distColisio;

			//si no arriba a la llum, sortirm del bucle
			if(distColisio < EPSILON){
				//continue;
				break;
			}
		
			if(distLlum < distColisio){
				lightsReached[j] = 1;
				//calcul especular
				vec3 half = normalize(direccioLlum + normalize(obs - puntcolisio));
				vec3 n = estimacioNormal(puntcolisio);
				if(dot(n, direccioLlum) > 0)
					specularIntensity[j] = clamp(dot(n, half), 0, 1);
			}
			profCercaLlum += distColisio;
		}
	}
}

vec2 rayMarching(vec3 obs, vec3 dir){
	//algorisme trobada del punt que pertany al fragment
	
	float profunditat = MIN_DIST;
	int material;
	float profunditatPasAnterior = 0;
	float colisioPasAnterior = 0;
	int paremRelaxation = 0;
	for(int i = 0; i <= MAX_MARCHING_STEPS; ++i){
		vec2 colisio = objectesEscena(obs + profunditat * dir);
		//comprovem si hem de parar over relaxation
		if(paremRelaxation == 0 && (abs(colisioPasAnterior)+abs(colisio.x) < colisioPasAnterior * relaxationIndex)) {
			//si aixo es compleix, cal tirar enrere
			profunditat = colisio.x + colisioPasAnterior*relaxationIndex*(1-relaxationIndex);
			i-= 1;
			relaxationIndex = 1;
			paremRelaxation = 1;
			continue;
		}

		float distColisio = colisio.x;
		material = int(colisio.y);
		if(distColisio < EPSILON){
			return vec2(profunditat, material);
		}
		profunditat += distColisio*relaxationIndex;
		if(profunditat >= MAX_DIST){
			return vec2(MAX_DIST, 0.);
		}
		//per calcular en el futur si hem de parar el metode d over relaxation
		colisioPasAnterior = colisio.x;

	}
	return vec2(profunditat, material);
}

float materialComponentReflexio(vec3 puntcolisio, vec3 obs){
	vec3 raigObsPunt = normalize(puntcolisio - obs);
	vec3 dirReflexio = reflect(raigObsPunt, estimacioNormal(puntcolisio));
	float profCercaReflexio =2*EPSILON;
	for(int i = 0; i < MAX_REFLECTION_STEPS; ++i){
		vec3 puntActual = puntcolisio + profCercaReflexio * dirReflexio;
		vec2 propietatsObjecteProper = objectesEscena(puntActual);
		float distColisio = propietatsObjecteProper.x;
		
		//cas que ja hem arribat practicament al objecte 
		if(distColisio < EPSILON){
			return propietatsObjecteProper.y;
		}
		
		profCercaReflexio += distColisio;
	}
	return -1;
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
	//calcul punt colisio
	vec2 prof = rayMarching(vObs, direction);
	float profunditat = prof.x;
	//calcul llums al punt
	vec3 puntcolisio = vObs + profunditat * direction;
	lightMarching(vObs, puntcolisio );
	float material = prof.y;

	//calcul obscurancia (ambient oclusion)
	vec3 normal = estimacioNormal(puntcolisio);
	vec3 pAux = puntcolisio + epsilonOcclusion*normal;
	obscurancia = (epsilonOcclusion-objectesEscena(pAux).x)/epsilonOcclusion;

	//calcul color
	if(profunditat < MAX_DIST){
		
		vec3 color = ambientColor(int(material)) * (1-obscurancia);
		vec4 infoSpecular = specularColor(int(material));
		for(int i = 0; i < llumsPuntuals.length(); ++i){
			color += infoSpecular.xyz * pow(specularIntensity[i], (infoSpecular.w*128)) * (lightsReached[i]*llumsPuntuals[i].w ); //"Multiply the shininess by 128!"
			//color += diffuseColor(int(material)) * (lightsReached[i]*llumsPuntuals[i].w ) * clamp(dot(normal, normalize(llumsPuntuals[i].xyz - puntcolisio)), 0, 1);
			//test ombres suaus
			color += diffuseColor(int(material)) * (lightsReached[i]*llumsPuntuals[i].w ) * clamp(dot(normal, normalize(llumsPuntuals[i].xyz - puntcolisio)), 0, 1) *min(dmin[i]/0.2, 1); 
		}
		//calcul component reflexio
		float materialColorReflex = materialComponentReflexio(puntcolisio, vObs);
		//com que no hi ha manera facil de saber exactament el color del objecte del que agafarem la reflexio
		//utilitzare alguna de les propietats del material
		//(1-c)*color pixel + c*color punt reflexat
		if(materialColorReflex > 0) color = (1-infoSpecular.w*0.5)*color + infoSpecular.w*0.5*ambientColor(int(materialColorReflex));
		FragColor = vec4(color, 1.0);
	}else{
		if(puntcolisio.y <= 0){
			FragColor = vec4(1, 0.75, 0.5, 1);
		}else{
			FragColor = vec4(puntcolisio.y/25, puntcolisio.y/12, 1, 1.0);
		}
	}
	//FragColor = vec4(1,1,0, 1.0);
	
}
