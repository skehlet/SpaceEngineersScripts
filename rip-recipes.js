// 2020-09-06: The XML format has changed, this script is not working
//
// cd to D:\SteamLibrary\SteamApps\common\SpaceEngineers\Content\Data then run me

var parseString = require('xml2js').parseString;
var fs = require('fs');
var Promise = require('bluebird');
var parseStringAsync = Promise.promisify(parseString)

var files = fs.readdirSync('.').filter(f => f.endsWith('.sbc'));

var subTypes = [];

// var bpnames = {
//     SteelPlate: "SteelPlate",
//     Construction: "ConstructionComponent",
//     PowerCell: "PowerCell",
//     Computer : "ComputerComponent" ,
//     LargeTube : "LargeTube",
//     Motor : "MotorComponent",
//     Display : "Display",
//     MetalGrid : "MetalGrid",
//     InteriorPlate : "InteriorPlate",
//     SmallTube  : "SmallTube",
//     RadioCommunication : "RadioCommunicationComponent",
//     BulletproofGlass  : "BulletproofGlass",
//     Girder : "GirderComponent",
//     Explosives: "ExplosivesComponent",
//     Detector : "DetectorComponent",
//     Medical : "MedicalComponent",
//     GravityGenerator : "GravityGeneratorComponent",
//     Superconductor : "Superconductor",
//     Thrust : "ThrustComponent",
//     Reactor : "ReactorComponent",
//     SolarCell : "SolarCell",
//     ZoneChip : "ZoneChip"
// }

// build up a block-type-keyed dictionary of all blocks then output them all at the end
var blockDefinitions = new Map();
// handler called once for each block subtype
// creates a list of subtypes under each type
function pushDefinition(type, subType, comps) {
    if (blockDefinitions.has(type)) {
        blockDefinitions.get(type).push(subType+"="+comps);
    } else {
        blockDefinitions.set(type, [subType+"="+comps]);
    }
}

Promise.mapSeries(files, function (file) {
    console.log(`Processing ${file}`);
    var xml = fs.readFileSync(file).toString();
    return parseStringAsync(xml).then(function (res) {
        var definitions = res.Definitions.CubeBlocks[0].Definition;
        for (let i = 0; i < definitions.length; i++) {
            let definition = definitions[i];
            var type = definition.Id[0].TypeId[0];
            if (type.substr(0, 16) == "MyObjectBuilder_") {
                // console.log(`YO! I stripped crap from ${type}`);
                type = type.substring(16);
            }
            let subType = definition.Id[0].SubtypeId[0];
            if (subType.length === 0) subType = "(null)";
            subType = subType.replace(" ", "");
            console.log(`${type} ${subType}`);

            var subTypeCounts = new Map();
            let components = definition.Components[0].Component;
            for (let j = 0; j < components.length; j++) {
                let component = components[j]['$'];
                console.log(`    ${component.Count} ${component.Subtype}`);
                let subTypeIndex = subTypes.indexOf(component.Subtype);
                if (subTypeIndex < 0) {
                    subTypes.push(component.Subtype);
                    subTypeIndex = subTypes.indexOf(component.Subtype);
                    // console.log(`First time seeing ${component.Subtype}, adding to subTypes array with index ${subTypeIndex}`);
                }

                if (subTypeCounts.has(subTypeIndex)) {
                    subTypeCounts.set(subTypeIndex, subTypeCounts.get(subTypeIndex) + Number(component.Count));
                } else {
                    subTypeCounts.set(subTypeIndex, Number(component.Count));
                }
            }
            var strComps = [];
            for (var [key, value] of subTypeCounts.entries()) {
                strComps.push(`${key}:${value}`);
            }
            console.log(`pushing ${type} ${subType} ${strComps.join(",")}`);
            pushDefinition(type, subType, strComps.join(","));
        }

    }).catch(function (e) {
        console.error(`Error in ${file}:`, e);
    });

}).then(function () {
    // subTypes.forEach(x => {
    //     if (!bpnames[x]) {
    //         console.log(`WARNING: No name mapping for ${x}`);
    //     }
    // });
    // subTypes = subTypes.map(x => bpnames[x]);
    process.stdout.write(subTypes.join("*"));

    for (var [key,value] of blockDefinitions.entries()) {
        process.stdout.write("$" + key + "*" + value.join("*"));
    }
}).catch(function (e) {
    console.error('Error:', e);
});
