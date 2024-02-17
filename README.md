PathCAM
=======

PathCAM - Toolpath generation software for CNC robots!  PathCAM is a simple, easy to use tool for generating 2.5D toolpaths to cut shapes from stock material using a CNC Router.  PathCAM can connect directly to some CNC robots, and can export simple .gcode for others.

This branch adds support for loading Gerber files (PCB board files) and isolation trace routing.  Help out by trying the tool and providing feedback and requesting more features!

![PathCAM Screenshot](https://raw.github.com/xenovacivus/PathCAM/master/Examples/screenshot-pcb.png)


Getting Started
---------------

* Ubuntu
  * Build & Run with these commands:
        
        ```
        sudo apt-get install mono-devel mono-gmcs
        git clone https://github.com/xenovacivus/PathCAM.git
        cd PathCAM
        xbuild
        mono GUI/bin/Debug/PathCAM.exe
        ```

* Windows
  * Download the [PathCAM MSI package (beta with Gerber support!)](https://github.com/xenovacivus/PathCAM/blob/pathcam-gerber/Installer/PathCAM.msi?raw=true)
  * Alternatively, you can build the sources with Mono or Visual Studio.


Usage (PCB isolation trace generation)
------------

Use the "Open" button to browse to Gerber files and open them.  PathCAM will load top/bottom copper, board edges, and drill files.  If you generated the files with a recent version of KiCad, PathCAM can automatically detect which files belong on which layers.  Otherwise, you can right-click on the loaded layer and reassign it to the proper location.

Use the fields on the left to set an isolation trace generation parameters:
 * Tool Diameter: diameter of the cutting tool (0.5mm by default)
 * Move Height: height of moves from one trace to another.  Any value above the board height will work.
 * Point Span: this is the distance between points on routed paths.  A larger value will be easier to send to CNC machines (fewer lines of GCode), but will have less resolution
 * Uniform Points: set to true to generate points uniformly spaced (even on straight lines).  This plays well with live updates of the z-offset for machines that can't inject a jog between buffered moves.
Additionally, for the board edge:
 * Max Cut Depth: the maximum depth the bit can cut in one pass
 * Last Pass Height: how far above or below the bottom of the board the last pass should go.  Set to a negative value to go beyond the board bottom (good if the PCB is held down with double-sided sticky tape), or a positive value to leave a small brim attached.
 * Note: currently there are no tabs inserted for PCB board edges (including internal cutouts).  Beware!

With the tool paths generated, you can save them to a .gcode file or run them directly from PathCAM on specific robots (Including machines running GRBL!!!).  Connections to more robots will be added in the future - if you have one in mind, say something and maybe it will be added sooner!

Connecting to a Robot
---------------------

There's a small checkbox hiding in the lower left-hand corner of PathCAM; click this to reveal some robot control buttons.  Here's the basic steps:

1. Robot Preconfiguration: if running GRBL, make sure your robot is set to Metric and that all the steps/mm and other settings are correct.  PathCAM will tell your robot to go to (0, 0, 0) when it connects, so make sure it's already around that location.
1. Click "Com Port" & select the COM port to which your robot is connected.  Choose the correct baud rate and click "Connect"; your robot should be automatically detected if it's running GRBL 8.0c or later and the GUI buttons for robot control will be enabled
1. Enter the material height in the "Z Go" box, and the negative of that value in the Z Offset box.  Hit return while in the Z Offset box, then use the mouse scroll wheel or up/down buttons to jog the Z axis to the surface of the material.
1. Load an object (.STL, .DAE, raw .gcode), ensure the speeds, heights, and other settings are to your liking, then click "Run" to start sending commands to your robot.
1. Sit back and enjoy!  If something starts going bad, try the "Pause" button and "Clear" buttons.  Doing Pause + Clear will bring the machine safely back to it's starting position (provided it hasn't missed any steps).

Special Thanks
--------------

OpenSource thrives on proper acknowledgement!  In that regard, I'd like to thank the authors of the following projects which play a critical part in PathCAM:

* [Clipper](http://www.angusj.com/delphi/clipper.php): A great polygon clipping tool - PathCAM uses Clipper to offset toolpaths from the raw sliced object layers.  Clipper is also used heavily in PathCAM's Gerber loader to merge the 2D art polygons.
* [STLdotNET](https://github.com/QuantumConcepts/STLdotNET): STL loading library used by PathCAM to open .STL files.  Works Great!
* [LibTessDotNet](https://github.com/speps/LibTessDotNet): Port of the GLU Tesselator - PathCAM uses LibTessDotNet to fill a polygon with triangles to be drawn by OpenGL.
* [Triangle](http://www.cs.cmu.edu/~quake/triangle.html) and [Triangle.NET](http://triangle.codeplex.com/): Awesome triangle tesselation tools, great for breaking up polygons with holes into a set of triangles.  PathCAM formerly used Triangle to tesselate polygons to be drawn by OpenGL.  Replaced by LibTessDotNet due to unexplainable license issues.

