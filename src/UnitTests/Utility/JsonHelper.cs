﻿using DuetAPI.Machine;
using DuetAPI.Utility;
using NUnit.Framework;
using System.Text.Json;

namespace DuetUnitTest.Utility
{
    [TestFixture]
    public class Json
    {
        // This is a 1:1 copy of DWC2's machine model with a RepRapPro Ormerod 2 config as generated by its poll connector
        private const string ormerodJson = "{\"electronics\": {\"type\": \"duetwifi10\",\"name\": \"Duet WiFi 1.0 or 1.01\",\"revision\": null,\"firmware\": {\"name\": \"RepRapFirmware for Duet 2 WiFi/Ethernet\",\"version\": \"2.02(RTOS)\",\"date\": \"2018-12-24b1\"},\"processorID\": null,\"vIn\": {\"current\": 0,\"min\": 0,\"max\": 0},\"mcuTemp\": {\"current\": 20.3,\"min\": 15.4,\"max\": 20.3},\"expansionBoards\": []},\"fans\": [{\"rpm\": 0,\"value\": 0.35,\"name\": \"\",\"thermostatic\": {\"control\": false,\"heaters\": [],\"temperature\": null},\"inverted\": false,\"frequency\": null,\"min\": 0,\"max\": 1,\"blip\": 0.1,\"pin\": null},{\"rpm\": null,\"value\": 1,\"name\": \"\",\"thermostatic\": {\"control\": true,\"heaters\": [],\"temperature\": null},\"inverted\": false,\"frequency\": null,\"min\": 0,\"max\": 1,\"blip\": 0.1,\"pin\": null},{\"rpm\": null,\"value\": 1,\"name\": \"\",\"thermostatic\": {\"control\": true,\"heaters\": [],\"temperature\": null},\"inverted\": false,\"frequency\": null,\"min\": 0,\"max\": 1,\"blip\": 0.1,\"pin\": null},{\"rpm\": null,\"value\": 0,\"name\": \"\",\"thermostatic\": {\"control\": true,\"heaters\": [],\"temperature\": null},\"inverted\": false,\"frequency\": null,\"min\": 0,\"max\": 1,\"blip\": 0.1,\"pin\": null},{\"rpm\": null,\"value\": 0,\"name\": \"\",\"thermostatic\": {\"control\": true,\"heaters\": [],\"temperature\": null},\"inverted\": false,\"frequency\": null,\"min\": 0,\"max\": 1,\"blip\": 0.1,\"pin\": null},{\"rpm\": null,\"value\": 0,\"name\": \"\",\"thermostatic\": {\"control\": true,\"heaters\": [],\"temperature\": null},\"inverted\": false,\"frequency\": null,\"min\": 0,\"max\": 1,\"blip\": 0.1,\"pin\": null},{\"rpm\": null,\"value\": 0,\"name\": \"\",\"thermostatic\": {\"control\": true,\"heaters\": [],\"temperature\": null},\"inverted\": false,\"frequency\": null,\"min\": 0,\"max\": 1,\"blip\": 0.1,\"pin\": null},{\"rpm\": null,\"value\": 0,\"name\": \"\",\"thermostatic\": {\"control\": true,\"heaters\": [],\"temperature\": null},\"inverted\": false,\"frequency\": null,\"min\": 0,\"max\": 1,\"blip\": 0.1,\"pin\": null},{\"rpm\": null,\"value\": 0,\"name\": \"\",\"thermostatic\": {\"control\": true,\"heaters\": [],\"temperature\": null},\"inverted\": false,\"frequency\": null,\"min\": 0,\"max\": 1,\"blip\": 0.1,\"pin\": null}],\"heat\": {\"beds\": [{\"active\": [0],\"standby\": [],\"name\": null,\"heaters\": [0]}],\"chambers\": [],\"coldExtrudeTemperature\": 160,\"coldRetractTemperature\": 90,\"extra\": [{\"current\": 20.3,\"name\": \"*MCU\",\"state\": null,\"sensor\": null},{\"current\": 20,\"name\": \"*DHT temperature\",\"state\": null,\"sensor\": null},{\"current\": 21,\"name\": \"*DHT humidity [%]\",\"state\": null,\"sensor\": null}],\"heaters\": [{\"current\": 19.7,\"name\": \"\",\"state\": 0,\"model\": {\"gain\": null,\"timeConst\": null,\"deadTime\": null,\"maxPwm\": null},\"max\": 290,\"sensor\": null},{\"current\": 18.2,\"name\": \"\",\"state\": 0,\"model\": {\"gain\": null,\"timeConst\": null,\"deadTime\": null,\"maxPwm\": null},\"max\": 290,\"sensor\": null},{\"current\": 17.7,\"name\": \"\",\"state\": 0,\"model\": {\"gain\": null,\"timeConst\": null,\"deadTime\": null,\"maxPwm\": null},\"max\": 290,\"sensor\": null},{\"current\": 2000,\"state\": 0,\"max\": 290,\"name\": \"\",\"model\": {\"gain\": null,\"timeConst\": null,\"deadTime\": null,\"maxPwm\": null},\"sensor\": null},{\"current\": 2000,\"state\": 0,\"max\": 290,\"name\": \"\",\"model\": {\"gain\": null,\"timeConst\": null,\"deadTime\": null,\"maxPwm\": null},\"sensor\": null},{\"current\": 2000,\"state\": 0,\"max\": 290,\"name\": \"\",\"model\": {\"gain\": null,\"timeConst\": null,\"deadTime\": null,\"maxPwm\": null},\"sensor\": null},{\"current\": 2000,\"state\": 0,\"max\": 290,\"name\": \"\",\"model\": {\"gain\": null,\"timeConst\": null,\"deadTime\": null,\"maxPwm\": null},\"sensor\": null},{\"current\": 2000,\"state\": 0,\"max\": 290,\"name\": \"\",\"model\": {\"gain\": null,\"timeConst\": null,\"deadTime\": null,\"maxPwm\": null},\"sensor\": null}]},\"job\": {\"fileName\": {\"name\": null,\"size\": null,\"filament\": [],\"generatedBy\": null,\"height\": null,\"layerHeight\": null,\"numLayers\": null,\"printTime\": null,\"simulatedTime\": null},\"filePosition\": null,\"lastFileName\": null,\"lastFileSimulated\": false,\"extrudedRaw\": [],\"duration\": null,\"layer\": null,\"layerTime\": null,\"layers\": [],\"warmUpDuration\": null,\"timesLeft\": {\"file\": null,\"filament\": null,\"layer\": null}},\"messageBox\": {\"mode\": null,\"title\": null,\"message\": null,\"timeout\": null,\"axisControls\": []},\"move\": {\"axes\": [{\"letter\": \"X\",\"drives\": [0],\"homed\": false,\"machinePosition\": 0,\"min\": 0,\"max\": 210,\"visible\": true},{\"letter\": \"Y\",\"drives\": [1],\"homed\": false,\"machinePosition\": 0,\"min\": 1,\"max\": 210,\"visible\": true},{\"letter\": \"Z\",\"drives\": [2],\"homed\": false,\"machinePosition\": 0,\"min\": 0,\"max\": 900,\"visible\": true}],\"babystepZ\": 0,\"currentMove\": {\"requestedSpeed\": 0,\"topSpeed\": 0},\"compensation\": \"None\",\"drives\": [{\"position\": 0,\"babystepping\": {\"value\": null,\"interpolated\": null},\"current\": 800,\"acceleration\": 900,\"minSpeed\": 12.5,\"maxSpeed\": 250},{\"position\": 0,\"babystepping\": {\"value\": null,\"interpolated\": null},\"current\": 1000,\"acceleration\": 900,\"minSpeed\": 12.5,\"maxSpeed\": 250},{\"position\": 0,\"babystepping\": {\"value\": null,\"interpolated\": null},\"current\": 800,\"acceleration\": 10,\"minSpeed\": 0.5,\"maxSpeed\": 4.5},{\"position\": 0,\"babystepping\": {\"value\": null,\"interpolated\": null},\"current\": 1000,\"acceleration\": 900,\"minSpeed\": 12.5,\"maxSpeed\": 100},{\"position\": 0,\"babystepping\": {\"value\": null,\"interpolated\": null},\"current\": 1000,\"acceleration\": 900,\"minSpeed\": 12.5,\"maxSpeed\": 100}],\"extruders\": [{\"factor\": 1,\"nonlinear\": {\"a\": 0,\"b\": 0,\"upperLimit\": 0.2,\"temperature\": 0}},{\"factor\": 1,\"nonlinear\": {\"a\": 0,\"b\": 0,\"upperLimit\": 0.2,\"temperature\": 0}}],\"geometry\": {\"type\": \"cartesian\"},\"idle\": {\"timeout\": 60,\"factor\": 30},\"speedFactor\": 1},\"network\": {\"name\": \"Ormerod\",\"password\": \"\",\"interfaces\": [{\"type\": \"wifi\",\"firmwareVersion\": \"1.21\",\"speed\": 0,\"signal\": null,\"configuredIP\": null,\"actualIP\": null,\"subnet\": null,\"gateway\": null,\"numReconnects\": null,\"activeProtocols\": []}]},\"scanner\": {\"progress\": 0,\"status\": \"D\"},\"sensors\": {\"endstops\": [{\"triggered\": false,\"position\": 0,\"type\": 0},{\"triggered\": false,\"position\": 0,\"type\": 0},{\"triggered\": false,\"position\": 0,\"type\": 0},{\"triggered\": false,\"position\": 0,\"type\": 0},{\"triggered\": true,\"position\": 0,\"type\": 0}],\"probes\": [{\"type\": 2,\"value\": 2,\"secondaryValues\": [2],\"threshold\": 500,\"speed\": 2,\"diveHeight\": 5,\"triggerHeight\": 1.3,\"inverted\": false,\"recoveryTime\": 0,\"travelSpeed\": 100,\"maxProbeCount\": 1,\"tolerance\": 0.03,\"disablesBed\": false}]},\"spindles\": [],\"state\": {\"isPrinting\": false,\"isSimulating\": null,\"atxPower\": false,\"currentTool\": -1,\"mode\": \"FFF\",\"status\": \"off\"},\"storages\": [{\"mounted\": true,\"speed\": null,\"capacity\": null,\"free\": null,\"openFiles\": null},{\"mounted\": false,\"speed\": null,\"capacity\": null,\"free\": null,\"openFiles\": null}],\"tools\": [{\"number\": 0,\"active\": [0],\"standby\": [0],\"name\": null,\"filament\": \"\",\"fans\": [0],\"heaters\": [1],\"extruders\": [0],\"mix\": [],\"spindle\": -1,\"axes\": [[0],[1]],\"offsets\": [-24.88,2,0]},{\"number\": 1,\"active\": [0],\"standby\": [0],\"name\": null,\"filament\": \"\",\"fans\": [0],\"heaters\": [2],\"extruders\": [1],\"mix\": [],\"spindle\": -1,\"axes\": [[0],[1]],\"offsets\": [1,2,0]}]}";

        // And this is a likely patch that can be applied to the ormerod config above (even though that reduces the number of heaters)
        private const string ormerodJsonPatch = "{\"heat\":{\"heaters\":[{\"current\":23.4},{\"current\":22.1},{\"current\":21.5}]}}";

        [Test]
        public void Read()
        {
            using JsonDocument parsedJson = JsonDocument.Parse(ormerodJson);
            MachineModel model = new MachineModel();
            JsonPatch.Patch(model, parsedJson);

            Assert.AreEqual("duetwifi10", model.Electronics.Type);
            Assert.AreEqual(3, model.Move.Axes.Count);
            Assert.AreEqual("Ormerod", model.Network.Name);
        }

        [Test]
        public void ReadAndPatch()
        {
            using JsonDocument parsedJson = JsonDocument.Parse(ormerodJson);
            MachineModel model = new MachineModel();
            JsonPatch.Patch(model, parsedJson);

            using JsonDocument patch = JsonDocument.Parse(ormerodJsonPatch);
            JsonPatch.Patch(model, patch);

            Assert.AreEqual(3, model.Heat.Heaters.Count);
            Assert.AreEqual(23.4, model.Heat.Heaters[0].Current, 0.0001);
            Assert.AreEqual(22.1, model.Heat.Heaters[1].Current, 0.0001);
            Assert.AreEqual(21.5, model.Heat.Heaters[2].Current, 0.0001);
        }

        [Test]
        public void Patch()
        {
            MachineModel a = new MachineModel();
            a.Electronics.Firmware.Name = "Foobar";
            a.Heat.Beds.Add(null);
            BedOrChamber bed2 = new BedOrChamber { Name = "BED2" };
            bed2.Standby.Add(20F);
            a.Heat.Beds.Add(bed2);
            a.Heat.Beds.Add(new BedOrChamber { Name = "BED3" });
            a.State.Status = MachineStatus.Busy;

            MachineModel b = new MachineModel();
            b.Electronics.Firmware.Name = "Foobar";
            BedOrChamber bed = new BedOrChamber { Name = "Bed" };
            bed.Active.Add(100F);
            b.Heat.Beds.Add(bed);
            BedOrChamber otherBed2 = new BedOrChamber { Name = "BED2" };
            otherBed2.Standby.Add(20F);
            b.Heat.Beds.Add(otherBed2);
            b.Fans.Add(new Fan { Value = 0.5F });
            b.State.Status = MachineStatus.Pausing;
            b.Scanner.Status = ScannerStatus.PostProcessing;

            byte[] patch = JsonPatch.Diff(a, b);
            using JsonDocument jsonDoc = JsonDocument.Parse(patch);
            JsonPatch.Patch(a, jsonDoc);

            Assert.AreEqual("Foobar", a.Electronics.Firmware.Name);
            Assert.AreEqual(2, a.Heat.Beds.Count);
            Assert.AreEqual("Bed", a.Heat.Beds[0].Name);
            Assert.AreEqual(new double[] { 100 }, a.Heat.Beds[0].Active);
            Assert.AreEqual("BED2", a.Heat.Beds[1].Name);
            Assert.AreEqual(new double[] { 20 }, a.Heat.Beds[1].Standby);
            Assert.AreEqual(1, a.Fans.Count);
            Assert.AreEqual(0.5, a.Fans[0].Value);
            Assert.AreEqual(MachineStatus.Pausing, a.State.Status);
            Assert.AreEqual(ScannerStatus.PostProcessing, a.Scanner.Status);
        }
    }
}
