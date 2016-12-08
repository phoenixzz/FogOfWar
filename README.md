# FogOfWar
Fog of War Effect for Unity5.x

About Fog of War
-------
Vision describes what a unit can and cannot see from its current location and state. Places a unit cannot see are filled with the Fog of War, a dark mist that hides units inside of it.
Invisible units cannot be seen even when outside of the Fog of War, unless they are spotted by a source of True Sight. Units cannot see through trees or to higher elevations.

All areas outside of friendly vision are constantly covered by the "Fog of War". Any non-friendly units within the Fog of War cannot be seen or targeted. 
To reveal units in the Fog of War, an allied unit must gain vision in that area. 

Example
-------
![FOW Pic1](https://github.com/phoenixzz/FogOfWar/blob/master/fow1.png)

![FOW Pic2](https://github.com/phoenixzz/FogOfWar/blob/master/fow2.png)

How To Use
-------
 - This tool depends on Unity's Projectors in Unity’s standard assets Package, therefore firstly you must Import Package -> Effects into your Project.
 - Import "FogOfWar" unitypackage or copy all files under Assets folder. All things done.
 - Create a empty GameObject in your scene and add "fow" component to it. The "fow" component also auto add "Projector" component for you if it is not added this component.
 You can set "Projector" component properties as you wish, but normally we set this GameObject Rotation X 90 degrees and use "Orthographic", setting suitable "Orthographic Size" to
 make the entire map under the cover of Projector.
 - Drag the "ShadowProjector" material under the "material" folder to Projector's material, keeping "Cookie" texture empty. You can also use any your "FallOff" texture as you wish.
 The "ShadowProjector" material uses modified "ProjectorMultiply" shader to achieve specific effects.
 - Change "Ignore Layers" property under "Projector" component to specify some certain layers not affected by Projector.
 - Under "fow" script, you can set default view radius and view beam property.  
  The "Dark Fog Gray" property can be used to achieve different fog of war effects:
    * 255   (normal fog of war effect: usually in the RPG game, used to mark the map walked through the region) (example pic 1)
    * 0      (Underground Palace effect: usually can only see the range around player and other field are dark)
    * other(< 128 usually)  (Field of View: usually used in RTS games to mark areas that friendly units can see) (example pic 2 like dota 2)
 - In your game scripts,
    * Firstly, you must get the "fow GameObject" in this map once after loading map, You can use "GameObject.FindGameObjectWithTag()" function and in the same time 
setting "fow GameObject" to one specific tag.
    * Secondly, If the map with static mask map, you should call "fow.InitMap()" function to set right mask width, height and mask data as byte array. 
The size of the mask diagram is the same as the size of the Projector's cookie texture. If the map without static mask map, you should generate mask data run-time.
    * Finally, when you like to start fog of war effect, you can call "fow.StartFow()" function to assign the FOW center (usually the player GameObject).
    * Before unloading map, you should call "fow.EndFow()" function to close fog of war effect and release resources.
 - Optimization
    * Reduce update rate, now setting refresh rate is o.5 second, you can change it in void FixedUpdate() function in Fow.cs.
    * Reduce jumpy-changing problem, now storing current updated result in red channel and last updated result in green channel of Projector's cookie texture, 
and interpolate transition the colour according to time in the shader. Also you can change 0.5s to any other value you like.
    * Use precalculated fov, now using "FOV_SHAPE_CIRCLE_PRECALCULATE" as default fov shape in void fov_setting_init() function in fov.cs.
 
Requires Unity5.x  or higher.

Developed By
-------
The original FOV(Field of Vision, Field of View) C++ algorithms comes from :
[libfov](https://sourceforge.net/projects/libfov/) in SourceForge, and I am porting to C# version.


developed by phoenixzz (Weng xiao yi) - <phoenixzz@sina.com>


License
-------
This program is free software, includes all source codes,You can freely modify and use it.