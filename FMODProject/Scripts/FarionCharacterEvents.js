/* Builds event:/Character/{Footstep,Land,Jump} from Assets/Character/Footsteps (Footsteps - Essentials library).
   Run headless from FMODProject/: fmodstudiocl -script Scripts/FarionCharacterEvents.js FarionAudio/FarionAudio.fspro
   (kept outside FarionAudio/Scripts so Studio does not auto-run it on project load)
   Re-running deletes and recreates the three events and the Surface parameter; Intensity/Wetness are reused.
   Surface label order must match Farion.Simulation.Planetary.FootstepSurface (locked by ExplorerFeelTests). */

(function () {
    var project = studio.project;
    var workspace = project.workspace;
    var assetsRoot = project.filePath.replace(/[\\\/][^\\\/]*$/, "") + "/Assets/";
    var runIntensity = 0.85;
    var silentDb = -80;

    var surfaces = [
        { label: "Rock", folder: "Rock",
          walk: ["Footsteps_Rock_Walk_", 9], run: ["Footsteps_Rock_Run_", 10],
          start: ["Footsteps_Rock_Jump_Start_", 6], land: ["Footsteps_Rock_Jump_Land_", 6] },
        { label: "Gravel", folder: "Gravel",
          walk: ["Footsteps_Gravel_Walk_", 10], run: ["Footsteps_Gravel_Run_", 10],
          start: ["Footsteps_Jump_Start_", 3], land: ["Footsteps_Jump_Land_", 3] },
        { label: "Sand", folder: "Sand",
          walk: ["Footsteps_Sand_Walk_", 20], run: ["Footsteps_Sand_Run_", 20],
          start: ["Footsteps_Sand_Jump_Start_", 5], land: ["Footsteps_Sand_Jump_Land_", 5] },
        { label: "Snow", folder: "Snow",
          walk: ["Footsteps_Snow_Walk_", 6], run: ["Footsteps_Snow_Run_", 10],
          start: ["Footsteps_Snow_Jump_Start_", 6], land: ["Footsteps_Snow_Jump_Land_", 6] },
        { label: "Ice", folder: "Snow",
          walk: ["Footsteps_Snow_Hard_Walk_", 12], run: ["Footsteps_Snow_Hard_Run_", 11],
          start: ["Footsteps_Snow_Hard_Jump_Start_", 5], land: ["Footsteps_Snow_Hard_Jump_Land_", 5] },
        { label: "Dirt", folder: "DirtyGround",
          walk: ["Footsteps_DirtyGround_Walk_", 10], run: ["Footsteps_DirtyGround_Run_", 10],
          start: ["Footsteps_DirtyGround_Jump_Start_", 3], land: ["Footsteps_DirtyGround_Jump_Land_", 3] },
        { label: "Grass", folder: "Grass",
          walk: ["Footsteps_Walk_Grass_Mono_", 50], run: ["Footsteps_Grass_Run_", 15],
          start: ["Footsteps_Grass_Jump_Start_", 10], land: ["Footsteps_Grass_Jump_Land_", 10] },
        { label: "Mud", folder: "Mud",
          walk: ["Footsteps_Mud_Walk_", 10], run: ["Footsteps_Mud_Run_", 7],
          start: ["DirtyGround/Footsteps_DirtyGround_Jump_Start_", 3], land: ["Footsteps_Mud_Jump_Land_", 5] },
        { label: "Metal", folder: "Metal",
          walk: ["Footsteps_MetalV1_Walk_", 15], run: ["Footsteps_MetalV1_Run_", 15],
          start: ["Footsteps_MetalV1_Jump_Start_", 4], land: ["Footsteps_MetalV1_Jump_Land_", 4] }
    ];
    var water = { folder: "Water",
        walk: ["Footsteps_WaterV1_Walk_", 10], run: ["Footsteps_Water_Run_", 5],
        start: ["Footsteps_Water_Jump_Light_", 6], land: ["Footsteps_Water_Jump_Big_", 3] };
    var surfaceLabels = surfaces.map(function (s) { return s.label; });

    function files(surface, setName) {
        var set = surface[setName];
        var stem = set[0];
        var folder = stem.indexOf("/") >= 0 ? "" : surface.folder + "/";
        var list = [];
        for (var i = 1; i <= set[1]; i++) {
            list.push("Character/Footsteps/" + folder + stem + (i < 10 ? "0" + i : i) + ".wav");
        }
        return list;
    }

    var assetsByPath = {};
    project.model.AudioFile.findInstances().forEach(function (asset) {
        assetsByPath[asset.getAssetPath()] = asset;
    });

    function audioFile(relativePath) {
        if (assetsByPath[relativePath]) {
            return assetsByPath[relativePath];
        }
        var imported = project.importAudioFile(assetsRoot + relativePath);
        if (!imported) {
            throw new Error("import failed: " + relativePath);
        }
        assetsByPath[relativePath] = imported;
        return imported;
    }

    function parameter(name, definition) {
        var preset = project.lookup("parameter:/" + name);
        if (preset) {
            return preset;
        }
        definition.name = name;
        var created = workspace.addGameParameter(definition);
        console.log("created parameter " + name);
        return created;
    }

    function eventFolder(name) {
        var existing = project.lookup("event:/" + name);
        if (existing) {
            return existing;
        }
        var folder = project.create("EventFolder");
        folder.name = name;
        folder.folder = workspace.masterEventFolder;
        return folder;
    }

    function deleteStale(path) {
        var stale = project.lookup(path);
        if (stale) {
            project.deleteObject(stale);
        }
    }

    function freshEvent(folder, name) {
        var event = workspace.addEvent(name, true);
        event.folder = folder;
        event.mixerInput.output = project.lookup("bus:/SFX");
        event.relationships.banks.add(project.lookup("bank:/Master"));
        return event;
    }

    function multi(track, parameterSheet, start, length, fileList, volumeDb, condition) {
        var instrument = track.addSound(parameterSheet, "MultiSound", start, length);
        fileList.forEach(function (relativePath) {
            var single = project.create("SingleSound");
            single.audioFile = audioFile(relativePath);
            instrument.relationships.sounds.add(single);
        });
        if (volumeDb) {
            instrument.volume = volumeDb;
        }
        if (condition) {
            instrument.addParameterCondition(condition[0], condition[1], condition[2]);
        }
        return instrument;
    }

    function automateVolume(mixerGroup, preset, points) {
        var curve = mixerGroup.addAutomator("volume").addAutomationCurve(preset.parameter);
        points.forEach(function (point) {
            curve.addAutomationPoint(point[0], point[1]);
        });
    }

    var dryByWetness = [[0, 0], [1, -14]];
    var wetByWetness = [[0, silentDb], [0.05, -18], [1, 0]];

    function surfaceTrack(event, sheet, name, set, volumeDb, condition, wetness) {
        var track = event.addGroupTrack(name);
        surfaces.forEach(function (surface, index) {
            multi(track, sheet, index, 1, files(surface, set), volumeDb, condition);
        });
        automateVolume(track.mixerGroup, wetness, dryByWetness);
        return track;
    }

    function waterTrack(event, sheet, name, set, volumeDb, condition, wetness) {
        var track = event.addGroupTrack(name);
        multi(track, sheet, 0, surfaces.length, files(water, set), volumeDb, condition);
        automateVolume(track.mixerGroup, wetness, wetByWetness);
        return track;
    }

    ["Footstep", "Land", "Jump"].forEach(function (name) {
        deleteStale("event:/Character/" + name);
    });
    deleteStale("parameter:/Surface");

    var surface = parameter("Surface", {
        type: project.parameterType.UserEnumeration, min: 0, max: surfaceLabels.length - 1,
        enumerationLabels: surfaceLabels
    });
    var intensity = parameter("Intensity", { type: project.parameterType.User, min: 0, max: 1 });
    var wetness = parameter("Wetness", { type: project.parameterType.User, min: 0, max: 1 });
    var character = eventFolder("Character");
    var walkCondition = [intensity, 0, runIntensity];
    var runCondition = [intensity, runIntensity, 1];

    var footstep = freshEvent(character, "Footstep");
    var footstepSheet = footstep.addGameParameter(surface);
    footstep.addGameParameter(intensity);
    footstep.addGameParameter(wetness);
    surfaceTrack(footstep, footstepSheet, "Walk", "walk", 0, walkCondition, wetness);
    surfaceTrack(footstep, footstepSheet, "Run", "run", 0, runCondition, wetness);
    waterTrack(footstep, footstepSheet, "Water Walk", "walk", 0, walkCondition, wetness);
    waterTrack(footstep, footstepSheet, "Water Run", "run", 0, runCondition, wetness);
    automateVolume(footstep.masterTrack.mixerGroup, intensity, [[0, -9], [1, 0]]);

    var land = freshEvent(character, "Land");
    var landSheet = land.addGameParameter(surface);
    land.addGameParameter(intensity);
    land.addGameParameter(wetness);
    surfaceTrack(land, landSheet, "Impact", "land", 0, null, wetness);
    waterTrack(land, landSheet, "Splash", "land", 0, null, wetness);
    automateVolume(land.masterTrack.mixerGroup, intensity, [[0, -12], [1, 0]]);

    var jump = freshEvent(character, "Jump");
    var jumpSheet = jump.addGameParameter(surface);
    jump.addGameParameter(wetness);
    surfaceTrack(jump, jumpSheet, "Push", "start", -6, null, wetness);
    waterTrack(jump, jumpSheet, "Splash", "start", -6, null, wetness);

    if (!project.save()) {
        throw new Error("project save failed");
    }
    console.log("Character events built: " + footstep.getPath() + ", " + land.getPath() + ", " + jump.getPath());
})();
