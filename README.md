Experimental code I've bodged together for importing some real world data into a ETS2 / ATS map.

<table border="0px">
 <tr>
   <td><img src="https://github.com/sk-zk/OsmProto/blob/master/screenshot1.png"></td>
   <td><img src="https://github.com/sk-zk/OsmProto/blob/master/screenshot2.png"></td>
   <td><img src="https://github.com/sk-zk/OsmProto/blob/master/screenshot3.png"></td>
  </tr>
</table>

This program
* imports roads from an OSM file (as outlines, not as actual road items),
* generates simple gray models of buildings from the same,
* places a terrain mesh (as close to that as I could get, anyway) with satellite imagery from Bing,
* loads elevation data for all of the above.

My abuse of game mechanics comes with some big drawbacks though, such as cluttering your asset lists with thousands of auto-generated materials and 
models that were necessary to make these screenshots happen.

The program is painfully slow even for small areas like a city center, so don't feed it an entire state, I guess.
(It'll also probably run out of memory after a few hours.)

Also, the low draw distance of the game is particularly disappointing with these maps. volvo pls fix.

## Usage
    OsmProto osm_file map_name out_dir

`osm_file`: Path to the OSM XML file to import from.  
`map_name`: Name of the generated map.  
`out_dir`: Output directory for the map and generated assets.

Remember to
1) place **[osm_proto_assets.zip](osm_proto_assets.zip)** in your mod folder
2) and **hit F8** when you load the map for the first time.

## Dependencies
* [AerialImageRetrieval](https://github.com/sk-zk/AerialImageRetrieval)
* [DEM.Net.Core](https://github.com/dem-net/DEM.Net)
* [LibTessDotNet](https://github.com/speps/LibTessDotNet)
* Microsoft.Extensions.Logging
* [TruckLib](https://github.com/sk-zk/TruckLib)
* [OsmSharp](https://github.com/OsmSharp/core)
