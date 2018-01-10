PathCAM
=======

PathCAM - Toolpath generation software for CNC robots!  PathCAM is a simple, easy to use tool for generating 2.5D toolpaths to cut shapes from stock material using a CNC Router.  PathCAM can connect directly to some CNC robots, and can export simple .gcode for others.

PathCAM is under active development!  Help out by trying the tool and providing feedback and requesting more features.

![PathCAM Screenshot](https://raw.github.com/xenovacivus/PathCAM/master/Examples/screenshot.png)


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
  * Download the [PathCAM MSI package](https://github.com/xenovacivus/PathCAM/blob/master/Installer/PathCAM.msi?raw=true)
  * Alternatively, you can build the sources with Mono or Visual Studio 2012.


Usage
------------

Start by loading a .stl or .obj file - you can just drag & drop from the file system, or use the Open File button.  Make sure the dropdown for scale is set correctly before loading the file (most files on Thingiverse are in millimeters).  You can move the models (green things) and the tabs (orange things) around to suit your needs.  Once you've got everything where you want it, try to generate some toolpaths.

* Boundary Check Paths: Adds a toolpath which follows the bounding box of the object at the safe moving height.  Useful to do a dry run and make sure the tool is clear of all clamps, etc.
* Add Perimeter Paths: Adds toolpaths which follow the edges of the object.  The paths will be divided into layers depending on "Max Cut Depth", and will do two passes along each edge: one rough cut, removing the bulk of the material, and a clean cut trimming the edge to the exact dimension.

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

* [Clipper](http://www.angusj.com/delphi/clipper.php): A great polygon clipping tool - PathCAM uses Clipper to offset toolpaths from the raw sliced object layers.
* [STLdotNET](https://github.com/QuantumConcepts/STLdotNET): STL loading library used by PathCAM to open .STL files.  Works Great!
* [Triangle](http://www.cs.cmu.edu/~quake/triangle.html) and [Triangle.NET](http://triangle.codeplex.com/): Awesome triangle tesselation tools, great for breaking up polygons with holes into a set of triangles.  PathCAM uses Triangle to tesselate polygons to be drawn by OpenGL.

