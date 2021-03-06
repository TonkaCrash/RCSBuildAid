/* Copyright © 2013-2016, Elián Hanisch <lambdae2@gmail.com>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace RCSBuildAid
{
    public static class DebugSettings
    {
        public static bool inFlightAngularInfo;
        public static bool inFlightPartInfo;
        public static bool labelMagnitudes;
        public static bool startInOrbit;

        public static bool inFlightInfo {
            get { return inFlightAngularInfo || inFlightPartInfo; }
        }
    }

    /* 
     * this never was satisfactory, but I don't know how to measure these values in flight better 
     */

#if DEBUG
    [KSPAddon(KSPAddon.Startup.Flight, false)]
#endif
    [RequireComponent(typeof(GUIText))]
    public class InFlightReadings : MonoBehaviour
    {
        Vessel vessel;
        float time;
        float longTime;

        double oldVel;
        double acc;
        double maxAcc;

        DebugVesselTree vesselTreeWindow;
        GUIText guiText;

        void Start ()
        {
            if (!DebugSettings.inFlightInfo && !DebugSettings.startInOrbit) {
                gameObject.SetActive(false);
                return;
            }

            guiText = gameObject.GetComponent<GUIText> ();
            guiText.transform.position = new Vector3 (0.82f, 0.94f, 0f);
            vessel = FlightGlobals.ActiveVessel;
            guiText.text = "no vessel";
        }

        void FixedUpdate ()
        {
            if (vessel == null) {
                return;
            }
            if (DebugSettings.startInOrbit && !vessel.packed && vessel.Landed) {
                toOrbit ();
                if (!DebugSettings.inFlightInfo) {
                    gameObject.SetActive(false);
                    return;
                }
            }
            if (DebugSettings.inFlightPartInfo && (vesselTreeWindow == null)) {
                vesselTreeWindow = gameObject.AddComponent<DebugVesselTree> ();
            }
            guiText.text = string.Empty;
            if (DebugSettings.inFlightAngularInfo) {
                double vel = vessel.angularVelocity.magnitude;
                time += TimeWarp.fixedDeltaTime;
                if (time > 0.1) {
                    acc = (vel - oldVel) / time;
                    maxAcc = Mathf.Max ((float)maxAcc, Mathf.Abs ((float)acc));
                    oldVel = vel;
                    time = 0;
                }
                longTime += TimeWarp.fixedDeltaTime;
                if (longTime > 10) {
                    maxAcc = 0;
                    longTime = 0;
                }
                //Vector3 MOI = vessel.findLocalMOI (vessel.CoM);
                Vector3 MOI = Vector3.zero;
                guiText.text += String.Format (
                    "angvel: {0}\n" +
                    "angmo: {1}\n" +
                    "rotation: {11}\n" +
                    "MOI: {2:F3} {3:F3} {4:F3}\n" +
                    "vel: {5:F5} rads {6:F5} degs\n" +
                    "acc: {7:F5} rads {8:F5} degs\n" +
                    "max: {9:F5} rads {10:F5} degs\n", 
                    vessel.angularVelocity,
                    vessel.angularMomentum,
                    MOI.x, MOI.y, MOI.z,
                    vel, toDeg (vel),
                    acc, toDeg (acc),
                    maxAcc, toDeg (maxAcc),
                    vessel.transform.rotation);
            }
        }

        double toDeg (double rad)
        {
            return rad * (180f / Math.PI);
        }

        void toOrbit ()
        {
            const double altitude = 11461728000; /* 10000m/s orbital speed, convenient for verify dV readings */
            CelestialBody body = Planetarium.fetch.Sun;
            Vessel vssl = FlightGlobals.ActiveVessel;
            vssl.Landed = false;
            vssl.Splashed = false;
            vssl.landedAt = String.Empty;
            for (int i = vssl.Parts.Count -1; i >= 0; i--) {
                Part part = vssl.Parts[i];
                if (part.FindModulesImplementing<LaunchClamp> ().Count != 0) {
                    part.Die ();
                }
            }
            vssl.GoOnRails();
            var orbit = new Orbit(0, 0, altitude + body.Radius, 0, 0, 0,
                                  Planetarium.GetUniversalTime(), body);
            vssl.orbitDriver.orbit = orbit;
            orbit.Init();
        }
    }


    /* add with ModuleManager */
    public class DragAverage : PartModule
    {
        [KSPField(guiActive = true, guiName = "Cd", guiFormat = "F2")]
        public float Cd;

        [KSPField(guiActive = true, guiName = "AvgCd", guiFormat = "F2")]
        public float AvgCd;

        List<float> values = new List<float> ();

        [KSPEvent(guiActive = true, guiName = "Clear average")]
        public void Clear()
        {
            values = new List<float> ();
        }

        void FixedUpdate()
        {
            Cd = part.DragCubes.AreaDrag;
            values.Add (Cd);
            float f = 0;
            for (int i = 0; i < values.Count; i++) {
                f += values [i];
            }
            AvgCd = f / values.Count;

            if (values.Count > 1000) {
                values.RemoveAt (0);
            }
        }
    }

    /* Automaticaly load the game and go to the editor or active vessel */
#if DEBUG
    //[KSPAddon(KSPAddon.Startup.MainMenu, false)]
#endif
    public class AutoStart : MonoBehaviour
    {
        static bool done;

        public void Start ()
        {
            if (done) {
                return;
            }

            HighLogic.SaveFolder = "default";
            Game game = GamePersistence.LoadGame("quicksave", HighLogic.SaveFolder, true, false);
            game.startScene = GameScenes.EDITOR;
            //game.startScene = GameScenes.FLIGHT;
            game.Start();
            done = true;
        }
    }
}

