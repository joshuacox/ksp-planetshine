﻿/*
* (C) Copyright 2014, Valerian Gaudeau
* 
* Kerbal Space Program is Copyright (C) 2013 Squad. See http://kerbalspaceprogram.com/. This
* project is in no way associated with nor endorsed by Squad.
* 
* This code is licensed under the Apache License Version 2.0. See the LICENSE.txt
* file for more information.
*/

using System.Collections.Generic;
using System.Collections;
using System;
using System.IO;
using UnityEngine;

namespace PlanetShine
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class VesselManager : MonoBehaviour
	{
		// local attributes
		private Config config = Config.Instance;
		public static VesselManager Instance { get; private set; }
		public static GameObject[] albedoLights;
		public static DynamicAmbientLight ambientLight;

		// for debug only
		private System.Diagnostics.Stopwatch performanceTimer = new System.Diagnostics.Stopwatch();
		private int performanceTimerStep = 0;

		public static LineRenderer debugLineLightDirection = null;
		public static LineRenderer debugLineSunDirection = null;
		public static LineRenderer debugLineBodyDirection = null;

		// for all of the calculations of UpdateAlbedoLights
        public CelestialBody body;
        public Color bodyColor;
		public float bodyGroundAmbient;
		public float bodyIntensity;
        public float bodyRadius;
		public Vector3 bodyVesselDirection;
		public Vector3 bodySunDirection;
		public float vesselAltitude;
		public float visibleSurface;
		public float sunAngle;
		public float visibleLightSunAngleMax;
		public float visibleLightSunAngleMin;
		public float visibleLightRatio;
		public float visibleLightAngleAverage;
		public float visibleLightAngleEffect;
		public float boostedVisibleLightAngleEffect;
		public Vector3 visibleLightPositionAverage;
		public float atmosphereReflectionEffect;
		public float atmosphereAmbientRatio;
		public float atmosphereAmbientEffect;
		public float areaSpreadAngle;
		public float areaSpreadAngleRatio;
		public float lightRange;
		public float vesselLightRangeRatio;
		public float lightDistanceEffect;
		public Vector3 visibleLightVesselDirection;
		public float lightIntensity;
 

		private void StartDebug()
		{
			debugLineLightDirection = Utils.CreateDebugLine(Color.white, Color.green);
			debugLineSunDirection = Utils.CreateDebugLine(Color.white, Color.yellow);
			debugLineBodyDirection = Utils.CreateDebugLine(Color.white, Color.red);
		}

		private void CreateAlbedoLights()
		{
			albedoLights = new GameObject[config.albedoLightsQuantity]; 
			for (var i = 0; i < config.albedoLightsQuantity; i++){
				if (albedoLights[i] != null)
					Destroy (albedoLights[i]);
				albedoLights[i] = new GameObject();
				albedoLights[i].AddComponent<Light>();
				albedoLights[i].light.type = LightType.Directional;
				albedoLights[i].light.cullingMask = (1 << 0);
				albedoLights[i].AddComponent<MeshRenderer>();
			}
		}

		private void UpdateAlbedoLights()
		{
			body = FlightGlobals.ActiveVessel.mainBody;
			bodyColor = new Color(100f/256f,100f/256f,100f/256f);
			bodyGroundAmbient = 0.3f;
			bodyIntensity = 1.0f;

			if (config.celestialBodyInfos.ContainsKey(body)) {
				bodyColor = config.celestialBodyInfos[body].albedoColor;
				bodyIntensity = config.celestialBodyInfos[body].albedoIntensity;
				bodyGroundAmbient = config.celestialBodyInfos[body].atmosphereAmbientLevel;
			}

			bodyRadius = (float) body.Radius * 0.99f;
			bodyVesselDirection = (FlightGlobals.ActiveVessel.transform.position - body.position).normalized;
			bodySunDirection = (body.name == "Sun") ? bodyVesselDirection : (Vector3) (FlightGlobals.Bodies[0].position - body.position).normalized;
			vesselAltitude = (float) (FlightGlobals.ActiveVessel.transform.position - body.position).magnitude - bodyRadius;
			visibleSurface = vesselAltitude / (float) (FlightGlobals.ActiveVessel.transform.position - body.position).magnitude;
			sunAngle = Vector3.Angle (bodySunDirection, bodyVesselDirection);
			visibleLightSunAngleMax = 90f + (90f * visibleSurface);
			visibleLightSunAngleMin = 90f - (90f * visibleSurface);
			visibleLightRatio = Mathf.Clamp01(((visibleLightSunAngleMax - sunAngle) / (visibleLightSunAngleMax - visibleLightSunAngleMin)));
			visibleLightAngleAverage = ((90f * visibleSurface) * (1f - (visibleLightRatio * (1f - (sunAngle / 180f)))));
			visibleLightAngleEffect = Mathf.Clamp01(1f - ((sunAngle - visibleLightAngleAverage) / 90f));
			boostedVisibleLightAngleEffect = Mathf.Clamp01(visibleLightAngleEffect + 0.3f);
			visibleLightPositionAverage = body.position + Vector3.RotateTowards(bodyVesselDirection, bodySunDirection, visibleLightAngleAverage * 0.01745f, 0.0f) * bodyRadius;
			atmosphereReflectionEffect = Mathf.Clamp01((1f - bodyGroundAmbient) + ((vesselAltitude - (bodyRadius * config.minAlbedoFadeAltitude)) / (bodyRadius * (config.maxAlbedoFadeAltitude - config.minAlbedoFadeAltitude))));
			atmosphereAmbientRatio = 1f - Mathf.Clamp01((vesselAltitude - (bodyRadius * config.minAmbientFadeAltitude)) / (bodyRadius * (config.maxAmbientFadeAltitude - config.minAmbientFadeAltitude)));
			atmosphereAmbientEffect = bodyGroundAmbient * config.baseGroundAmbient * atmosphereAmbientRatio;
			areaSpreadAngle = (1f + (0.4f * atmosphereAmbientRatio)) * config.areaSpreadAngleMax * Mathf.Clamp01((bodyRadius / ((visibleLightPositionAverage - FlightGlobals.ActiveVessel.transform.position).magnitude * 2))) * (visibleLightRatio * (1f - (sunAngle / 180f)));
			areaSpreadAngleRatio = Mathf.Clamp01(areaSpreadAngle / config.areaSpreadAngleMax);
			lightRange = bodyRadius * config.albedoRange;
			vesselLightRangeRatio = (float) vesselAltitude / lightRange;
			lightDistanceEffect = 1.0f / (1.0f + 25.0f * vesselLightRangeRatio * vesselLightRangeRatio);
			visibleLightVesselDirection = (FlightGlobals.ActiveVessel.transform.position - visibleLightPositionAverage).normalized;

			if (config.debug) {
				print ("PlanetShine: body " + body.name);
				print ("PlanetShine: vesselAltitude " + vesselAltitude);
				print ("PlanetShine: visibleSurface " + visibleSurface);
				print ("PlanetShine: sunAngle " + sunAngle);
				print ("PlanetShine: visibleLightSunAngleMax " + visibleLightSunAngleMax);
				print ("PlanetShine: visibleLightSunAngleMin " + visibleLightSunAngleMin);
				print ("PlanetShine: visibleLightAngleAverage " + visibleLightAngleAverage);
				print ("PlanetShine: visibleLightPositionAverage " + visibleLightPositionAverage);
				print ("PlanetShine: atmosphereAmbientEffect " + atmosphereAmbientEffect);
				print ("PlanetShine: areaSpreadAngle " + areaSpreadAngle);
				print ("PlanetShine: areaSpreadIntensityMultiplicator " + config.areaSpreadIntensityMultiplicator);
				print ("PlanetShine: lightRange " + lightRange);
				print ("PlanetShine: vesselLightRangeRatio " + vesselLightRangeRatio);
				print ("PlanetShine: visibleLightRatio " + visibleLightRatio);
				print ("PlanetShine: visibleLightAngleEffect " + visibleLightAngleEffect);
				print ("PlanetShine: boostedVsibleLightAngleEffect " + visibleLightAngleEffect);
				print ("PlanetShine: atmosphereReflectionEffect " + atmosphereReflectionEffect);
				print ("PlanetShine: lightDistanceEffect " + lightDistanceEffect);

				debugLineLightDirection.SetPosition( 0, visibleLightPositionAverage );
				debugLineLightDirection.SetPosition( 1, FlightGlobals.ActiveVessel.transform.position );

				debugLineSunDirection.SetPosition( 0, FlightGlobals.Bodies[0].position );
				debugLineSunDirection.SetPosition( 1, FlightGlobals.ActiveVessel.transform.position );

				debugLineBodyDirection.SetPosition( 0, body.position );
				debugLineBodyDirection.SetPosition( 1, FlightGlobals.ActiveVessel.transform.position );

			}
				
			lightIntensity = config.baseAlbedoIntensity / config.albedoLightsQuantity;
			lightIntensity *= visibleLightRatio * boostedVisibleLightAngleEffect * atmosphereReflectionEffect * lightDistanceEffect * bodyIntensity;

			if (config.debug)
				print ("PlanetShine: INTENSITY " + lightIntensity);

			int i = 0;
			foreach (GameObject albedoLight in albedoLights){
				albedoLight.light.intensity = lightIntensity;
				albedoLight.light.transform.forward = visibleLightVesselDirection;
				if (config.albedoLightsQuantity > 1 ) { // Spread the lights, but only if there are more than one
					albedoLight.light.transform.forward = Quaternion.AngleAxis (areaSpreadAngle, Vector3.Cross (bodyVesselDirection, bodySunDirection).normalized) * albedoLight.light.transform.forward;
					albedoLight.light.transform.forward = Quaternion.AngleAxis (i * (360f / config.albedoLightsQuantity), bodyVesselDirection) * albedoLight.light.transform.forward;
					albedoLight.light.intensity *= 1f + (areaSpreadAngleRatio * areaSpreadAngleRatio * config.areaSpreadIntensityMultiplicator);
				}
				albedoLight.light.color = bodyColor;
				albedoLight.light.enabled = true;
				i++;
			}

			if (ambientLight != null) {
				ambientLight.vacuumAmbientColor = atmosphereAmbientEffect * visibleLightAngleEffect * bodyColor + new Color(config.vacuumLightLevel,config.vacuumLightLevel,config.vacuumLightLevel);
				if (config.debug)
					print ("PlanetShine: Vacuum level " + config.vacuumLightLevel);
			}
		}



		public void Start()
		{
			if (Instance != null)
				Destroy (Instance.gameObject);
			Instance = this;

			ambientLight = FindObjectOfType(typeof(DynamicAmbientLight)) as DynamicAmbientLight;

			if (ambientLight != null) {
				ambientLight.vacuumAmbientColor = new Color(config.vacuumLightLevel,config.vacuumLightLevel,config.vacuumLightLevel);
			}

			CreateAlbedoLights ();

			if (config.debug)
				StartDebug ();
		}

		public void Update()
		{
			if (config.debug) {
				performanceTimerStep = 0;
				performanceTimer.Reset();
				performanceTimer.Start();
			}

			UpdateAlbedoLights();

			if (config.debug) {
				performanceTimer.Stop();
				print ("PlanetShine: total update time " + performanceTimer.Elapsed.TotalMilliseconds);

				/*if (Input.GetKeyUp (KeyCode.PageUp) == true) {
					lightsOn = !lightsOn;
				}*/
			}
		}

		private void TimerLog()
		{
			if (!config.debug)
				return;
			print ("PlanetShine: timer #" + performanceTimerStep++ + " : " + performanceTimer.Elapsed.TotalMilliseconds);
		}
	}
}