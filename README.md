PathCAM
=======

PathCAM - Toolpath generation software for CNC robots!  PathCAM is a simple, easy to use tool for generating 2.5D toolpaths to cut shapes from stock material using a CNC Router.  PathCAM can connect directly to some CNC robots, and can export simple .gcode for others.

PathCAM is under active development!  Help out by trying the tool and providing feedback and requesting more features.

![PathCAM Screenshot](https://raw.github.com/xenovacivus/PathCAM/master/Examples/screenshot.png)


Getting Started
---------------

* From Source: You'll need [Visual Studio 2012](Code http://www.microsoft.com/en-us/download/details.aspx?id=34673) to build the code.
* From Installer: Download & Install the [PathCAM MSI package](https://github.com/xenovacivus/PathCAM/blob/master/Installer/PathCAM.msi?raw=true).

Start by loading a .stl or .obj file - you can just drag & drop from the file system, or use the Open File button.  Make sure the dropdown for scale is set correctly before loading the file, otherwise you might wind up with something that you can't see or that takes up the entire screen!  You can move the models (green things) and the tabs (orange things) around to suit your needs.  Once you've got everything where you want it, try to generate some toolpaths.

* Boundary Check Paths: Adds a toolpath which follows the bounding box of the object at the safe moving height.  Useful to do a dry run and make sure the tool is clear of all clamps, etc.
* Add Perimeter Paths: Adds toolpaths which follow the edges of the object.  The paths will be divided into layers depending on "Max Cut Depth", and will do two passes along each edge: one rough cut, removing the bulk of the material, and a clean cut trimming the edge to the exact dimension.

With the tool paths generated, you can save them to a .gcode file or run them directly from PathCAM on specific robots (right now, it's VERY specific - only 2 robots in the entire world...).  Connections to more robots will be added in the future - if you have one in mind, say something and maybe it will be added sooner!


Special Thanks
--------------

OpenSource thrives on proper acknowledgement!  In that regard, I'd like to thank the authors of the following projects which play a critical part in PathCAM:

* [Clipper](http://www.angusj.com/delphi/clipper.php): A great polygon clipping tool - PathCAM uses Clipper to offset toolpaths from the raw sliced object layers.
* [STLdotNET](https://github.com/QuantumConcepts/STLdotNET): STL loading library used by PathCAM to open .STL files.  Works Great!
* [Triangle](http://www.cs.cmu.edu/~quake/triangle.html) and [Triangle.NET](http://triangle.codeplex.com/): Awesome triangle tesselation tools, great for breaking up polygons with holes into a set of triangles.  PathCAM uses Triangle to tesselate polygons to be drawn by OpenGL.

