/* Builds event:/Character/{Footstep,Land,Jump} from Assets/Character.
   Run headless from FMODProject/: fmodstudiocl -script Scripts/FarionCharacterEvents.js FarionAudio/FarionAudio.fspro
   (kept outside FarionAudio/Scripts so Studio does not auto-run it on project load)
   Re-running deletes and recreates the three events; parameters are reused. */

(function () {
    var project = studio.project;
    var workspace = project.workspace;
    var assetsRoot = project.filePath.replace(/[\\\/][^\\\/]*$/, "") + "/Assets/";

    var surfaceLabels = ["Rock", "Regolith", "Soil", "Ice", "Metal"];
    var footstepFiles = {
        Rock: ["footstep_concrete_walk_01", "footstep_concrete_walk_02", "footstep_concrete_walk_03",
               "footstep_concrete_run_02", "footstep_concrete_run_04"],
        Regolith: ["footstep_gravel_walk_01", "footstep_gravel_walk_02", "footstep_gravel_walk_03",
                   "footstep_gravel_run_01", "footstep_gravel_run_02", "footstep_gravel_run_03"],
        Soil: ["footstep_dirt_walk_run_01", "footstep_dirt_walk_run_02", "footstep_dirt_walk_run_03",
               "footstep_dirt_loose_walk_run_01", "footstep_dirt_loose_walk_run_02", "footstep_dirt_loose_walk_run_03"],
        Metal: ["footstep_wood_walk_01", "footstep_wood_walk_02", "footstep_wood_walk_03",
                "footstep_wood_run_01", "footstep_wood_run_02", "footstep_wood_run_03"]
    };
    var surfaceFolder = { Rock: "Rock", Regolith: "Regolith", Soil: "Soil", Ice: "Rock", Metal: "Metal" };
    var surfaceFiles = { Rock: footstepFiles.Rock, Regolith: footstepFiles.Regolith, Soil: footstepFiles.Soil,
                         Ice: footstepFiles.Rock, Metal: footstepFiles.Metal };

    function audioFile(relativePath) {
        var existing = workspace.masterAssetFolder.getAsset(relativePath);
        if (existing) {
            return existing;
        }
        var imported = project.importAudioFile(assetsRoot + relativePath);
        if (!imported) {
            throw new Error("import failed: " + relativePath);
        }
        console.log("imported " + imported.getAssetPath());
        return imported;
    }

    function parameter(name, definition) {
        var preset = project.lookup("parameter:/" + name);
        if (preset) {
            return preset;
        }
        definition.name = name;
        var created = workspace.addGameParameter(definition);
        console.log("created parameter " + name + " (" + created.entity + ")");
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

    function freshEvent(folder, name) {
        var stale = project.lookup("event:/" + folder.name + "/" + name);
        if (stale) {
            project.deleteObject(stale);
        }
        var event = workspace.addEvent(name, true);
        event.folder = folder;
        event.mixerInput.output = project.lookup("bus:/SFX");
        event.relationships.banks.add(project.lookup("bank:/Master"));
        return event;
    }

    function sheet(event, preset) {
        var proxy = event.addGameParameter(preset);
        return proxy;
    }

    function multi(track, parameterSheet, start, length, files, volumeDb) {
        var instrument = track.addSound(parameterSheet, "MultiSound", start, length);
        files.forEach(function (relativePath) {
            var single = project.create("SingleSound");
            single.audioFile = audioFile(relativePath);
            instrument.relationships.sounds.add(single);
        });
        if (volumeDb) {
            instrument.volume = volumeDb;
        }
        return instrument;
    }

    function volumeByIntensity(event, intensityPreset, lowDb) {
        var automator = event.masterTrack.mixerGroup.addAutomator("volume");
        var curve = automator.addAutomationCurve(intensityPreset.parameter);
        curve.addAutomationPoint(0, lowDb);
        curve.addAutomationPoint(1, 0);
    }

    var surface = parameter("Surface", {
        type: project.parameterType.UserEnumeration, min: 0, max: surfaceLabels.length - 1,
        enumerationLabels: surfaceLabels
    });
    var intensity = parameter("Intensity", { type: project.parameterType.User, min: 0, max: 1 });
    var wetness = parameter("Wetness", { type: project.parameterType.User, min: 0, max: 1 });
    var character = eventFolder("Character");

    var footstep = freshEvent(character, "Footstep");
    var footstepSurface = sheet(footstep, surface);
    sheet(footstep, intensity);
    sheet(footstep, wetness);
    var steps = footstep.addGroupTrack("Steps");
    surfaceLabels.forEach(function (label, index) {
        var files = surfaceFiles[label].map(function (file) {
            return "Character/Footsteps/" + surfaceFolder[label] + "/" + file + ".wav";
        });
        multi(steps, footstepSurface, index, 1, files, 0);
    });
    volumeByIntensity(footstep, intensity, -9);

    var land = freshEvent(character, "Land");
    sheet(land, surface);
    sheet(land, intensity);
    var impact = land.addGroupTrack("Impact");
    multi(impact, land.timeline, 0, 1, ["Character/Land/footstep_concrete_land_01.wav"], 0);
    volumeByIntensity(land, intensity, -12);

    var jump = freshEvent(character, "Jump");
    var push = jump.addGroupTrack("Push");
    multi(push, jump.timeline, 0, 1, footstepFiles.Soil.slice(3).map(function (file) {
        return "Character/Footsteps/Soil/" + file + ".wav";
    }), -8);

    if (!project.save()) {
        throw new Error("project save failed");
    }
    console.log("Character events built: " + footstep.getPath() + ", " + land.getPath() + ", " + jump.getPath());
})();
