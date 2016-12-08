using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


/**
 * Field of View Library
 * 
 * 
 * This is a C library which implements a course-grained lighting
 * algorithm suitable for tile-based games such as roguelikes.
 * 
 * 
 * Thanks to Bj&ouml;rn Bergstr&ouml;m
 * <bjorn.bergstrom@hyperisland.se> for the algorithm.
 * 
 */

/*
+---++---++---++---+
|   ||   ||   ||   |
|   ||   ||   ||   |
|   ||   ||   ||   |
+---++---++---++---+    2
+---++---++---+#####
|   ||   ||   |#####
|   ||   ||   |#####
|   ||   ||   |#####
+---++---++---+#####X 1 <-- y
+---++---++---++---+
|   ||   ||   ||   |
| @ ||   ||   ||   |       <-- srcy centre     -> dy = 0.5 = y - 0.5
|   ||   ||   ||   |
+---++---++---++---+    0
0       1       2       3       4
    ^                       ^
    |                       |
 srcx                   x            -> dx = 3.5 = x + 0.5
centre

Slope from @ to X.

+---++---++---++---+
|   ||   ||   ||   |
|   ||   ||   ||   |
|   ||   ||   ||   |
+---++---++---++---+ 2
+---++---++---++---+
|   ||   ||   ||   |
|   ||   ||   ||   |
|   ||   ||   ||   |
+---++---++---+X---+ 1   <-- y
+---++---++---+#####
|   ||   ||   |#####
| @ ||   ||   |#####      <-- srcy centre     -> dy = 0.5 = y - 0.5
|   ||   ||   |#####
+---++---++---+##### 0
0       1       2       3
    ^                       ^
    |                       |
 srcx                   x            -> dx = 2.5 = x - 0.5
centre

Slope from @ to X
*/


namespace Game.Tools.FOV
{
    /** Eight-way directions. */
    public enum fov_direction_type
    {
        FOV_EAST = 0,
        FOV_NORTHEAST,
        FOV_NORTH,
        FOV_NORTHWEST,
        FOV_WEST,
        FOV_SOUTHWEST,
        FOV_SOUTH,
        FOV_SOUTHEAST,
    }

    /** Values for the shape setting. */ 
    public enum fov_shape_type
    {
        FOV_SHAPE_CIRCLE_PRECALCULATE,
        FOV_SHAPE_SQUARE,
        FOV_SHAPE_CIRCLE,
        FOV_SHAPE_OCTAGON,
    }

    /** Values for the corner peek setting. */
    public enum fov_corner_peek_type
    {
        FOV_CORNER_NOPEEK,
        FOV_CORNER_PEEK,
    }

    /** Values for the opaque apply setting. */
    public enum fov_opaque_apply_type
    {
        FOV_OPAQUE_APPLY,
        FOV_OPAQUE_NOAPPLY,
    }

    public enum fov_octants_part
    {
        FOV_OCTANT_PPN,
        FOV_OCTANT_PPY,
        FOV_OCTANT_PMN,
        FOV_OCTANT_PMY,
        FOV_OCTANT_MPN,
        FOV_OCTANT_MPY,
        FOV_OCTANT_MMN,
        FOV_OCTANT_MMY,
    }


    class fov
    {
        /** Opacity test callback. */
        public delegate bool OnOpaqueEventHandler(map _map, int x, int y);
        /** Lighting callback to set lighting on a map tile. */
        public delegate void OnApplyEventHandler(map _map, int x, int y, int dx, int dy, Color src);


        public class fov_settings_type
        {
            /** Shape setting. */
            public fov_shape_type shape;
            public fov_corner_peek_type corner_peek;
            public fov_opaque_apply_type opaque_apply;

            public OnOpaqueEventHandler m_OnOpaqueEventHandler = null;
            public OnApplyEventHandler m_OnApplyEventHandler = null;

            /* Pre-calculated data. */
            public Dictionary<uint, List<uint>> heights = new Dictionary<uint,List<uint>>();
            /* Size of pre-calculated data. */
            public uint numheights = 0;
        }

        public struct fov_private_data_type
        {
            public fov_settings_type settings;
            public map _map;
            public Color source;
            public int source_x;
            public int source_y;
            public uint radius;
        }


        /**
        * Set all the default options. You must call this option when you
        * create a new settings data structure.
        *
        * These settings are the defaults used:
        *
        * - shape: FOV_SHAPE_CIRCLE_PRECALCULATE
        * - corner_peek: FOV_CORNER_NOPEEK
        * - opaque_apply: FOV_OPAQUE_APPLY
        *
        * Callbacks still need to be set up after calling this function.
        *
        * \param settings data structure containing settings.
        */
        public void fov_settings_init(fov_settings_type settings)
        {
            settings.shape = fov_shape_type.FOV_SHAPE_CIRCLE_PRECALCULATE;
            settings.corner_peek = fov_corner_peek_type.FOV_CORNER_NOPEEK;
            settings.opaque_apply = fov_opaque_apply_type.FOV_OPAQUE_APPLY;
            settings.m_OnOpaqueEventHandler = null;
            settings.m_OnApplyEventHandler = null;
            settings.heights.Clear();
            settings.numheights = 0;
        }

        /**
            * Set the shape of the field of view.
            *
            * \param settings  data structure containing settings.
            * \param value One of the following values, where R is the radius:
            *
            * - FOV_SHAPE_CIRCLE_PRECALCULATE \b (default): Limit the FOV to a
            * circle with radius R by precalculating, which consumes more memory
            * at the rate of 4*(R+2) bytes per R used in calls to fov_circle. 
            * Each radius is only calculated once so that it can be used again. 
            * Use fov_free() to free this precalculated data's memory.
            *
            * - FOV_SHAPE_CIRCLE: Limit the FOV to a circle with radius R by
            * calculating on-the-fly.
            *
            * - FOV_SHAPE_OCTOGON: Limit the FOV to an octogon with maximum radius R.
            *
            * - FOV_SHAPE_SQUARE: Limit the FOV to an R*R square.
            */
        public void fov_settings_set_shape(fov_settings_type settings, fov_shape_type value)
        {
            settings.shape = value;
        }

        /**
         * <em>NOT YET IMPLEMENTED</em>.
         *
         * Set whether sources will peek around corners.
         *
         * \param settings data structure containing settings.
         * \param value One of the following values:
         *
         * - FOV_CORNER_PEEK \b (default): Renders:
        \verbatim
          ........
          ........
          ........
          ..@#    
          ...#    
        \endverbatim
         * - FOV_CORNER_NOPEEK: Renders:
        \verbatim
          ......
          .....
          ....
          ..@#
          ...#
        \endverbatim
         */
        public void fov_settings_set_corner_peek(fov_settings_type settings, fov_corner_peek_type value)
        {
            settings.corner_peek = value;
        }

        /**
        * Whether to call the apply callback on opaque tiles.
        *
        * \param settings  data structure containing settings.
        * \param value One of the following values:
        *
        * - FOV_OPAQUE_APPLY \b (default): Call apply callback on opaque tiles.
        * - FOV_OPAQUE_NOAPPLY: Do not call the apply callback on opaque tiles.
        */
        public void fov_settings_set_opaque_apply(fov_settings_type settings, fov_opaque_apply_type value)
        {
            settings.opaque_apply = value;
        }

        /**
        * Set the function used to test whether a map tile is opaque.
        *
        * \param settings data structure containing settings.
        * \param f The function called to test whether a map tile is opaque.
        */
        public void fov_settings_set_opacity_test_function(fov_settings_type settings, OnOpaqueEventHandler eventHandler)
        {
            settings.m_OnOpaqueEventHandler = eventHandler;
        }

        /**
         * Set the function used to apply lighting to a map tile.
         *
         * \param settings data structure containing settings.
         * \param f The function called to apply lighting to a map tile.
         */
        public void fov_settings_set_apply_lighting_function(fov_settings_type settings, OnApplyEventHandler eventHandler)
        {
            settings.m_OnApplyEventHandler = eventHandler;
        }

        /**
         * Free any memory that may have been cached in the settings
         * structure.
         *
         * \param settings  data structure containing settings.
         */
        public void fov_settings_free(fov_settings_type settings)
        {
            settings.heights.Clear();
            settings.numheights = 0;
        }

        /**
         * Calculate a full circle field of view from a source at (x,y).
         *
         * \param settings  data structure containing settings.
         * \param map map data structure to be passed to callbacks.
         * \param source  data structure holding source of light.
         * \param source_x x-axis coordinate from which to start.
         * \param source_y y-axis coordinate from which to start.
         * \param radius Euclidean distance from (x,y) after which to stop.
         */
        public void fov_circle(fov_settings_type settings, map _map, Color source, int source_x, int source_y, uint radius)
        {
            fov_private_data_type data = new fov_private_data_type();

            data.settings = settings;
            data._map = _map;
            data.source = source;
            data.source_x = source_x;
            data.source_y = source_y;
            data.radius = radius;

            _fov_circle(data);
        }

        /**
         * Calculate a field of view from source at (x,y), pointing
         * in the given direction and with the given angle. The larger
         * the angle, the wider, "less focused" the beam. Each side of the
         * line pointing in the direction from the source will be half the
         * angle given such that the angle specified will be represented on
         * the raster.
         *
         * \param settings data structure containing settings.
         * \param map  map data structure to be passed to callbacks.
         * \param source data structure holding source of light.
         * \param source_x x-axis coordinate from which to start.
         * \param source_y y-axis coordinate from which to start.
         * \param radius Euclidean distance from (x,y) after which to stop.
         * \param direction One of eight directions the beam of light can point.
         * \param angle The angle at the base of the beam of light, in degrees.
         */
        public void fov_beam(fov_settings_type settings, map _map, Color source, int source_x, int source_y,
                            uint radius, fov_direction_type direction, float angle)
        {
            fov_private_data_type data = new fov_private_data_type();
            float start_slope, end_slope, a;

            data.settings = settings;
            data._map = _map;
            data.source = source;
            data.source_x = source_x;
            data.source_y = source_y;
            data.radius = radius;

            if (angle <= 0.0f)
            {
                return;
            }
            else if (angle >= 360.0f)
            {
                _fov_circle(data);
            }

            /* Calculate the angle as a percentage of 45 degrees, halved (for
             * each side of the centre of the beam). e.g. angle = 180.0f means
             * half the beam is 90.0 which is 2x45, so the result is 2.0.
             */
            a = angle / 90.0f;

            if (direction == fov_direction_type.FOV_EAST)
            {
                end_slope = betweenf(a, 0.0f, 1.0f);
                _fov_octant_part(fov_octants_part.FOV_OCTANT_PPN, data, 1, 0.0f, end_slope);
                _fov_octant_part(fov_octants_part.FOV_OCTANT_PMN, data, 1, 0.0f, end_slope);

                if (a - 1.0f > Mathf.Epsilon)
                { /* a > 1.0f */
                    start_slope = betweenf(2.0f - a, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PPY, data, 1, start_slope, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MPY, data, 1, start_slope, 1.0f);
                }
                if (a - 2.0f > Mathf.Epsilon)
                { /* a > 2.0f */
                    end_slope = betweenf(a - 2.0f, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PMY, data, 1, 0.0f, end_slope);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MMY, data, 1, 0.0f, end_slope);
                }
                if (a - 3.0f > Mathf.Epsilon)
                { /* a > 3.0f */
                    start_slope = betweenf(4.0f - a, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MPN, data, 1, start_slope, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MMN, data, 1, start_slope, 1.0f);
                }
            }
            if (direction == fov_direction_type.FOV_WEST)
            {
                end_slope = betweenf(a, 0.0f, 1.0f);
                _fov_octant_part(fov_octants_part.FOV_OCTANT_MPN, data, 1, 0.0f, end_slope);
                _fov_octant_part(fov_octants_part.FOV_OCTANT_MMN, data, 1, 0.0f, end_slope);

                if (a - 1.0f > Mathf.Epsilon)
                { /* a > 1.0f */
                    start_slope = betweenf(2.0f - a, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PMY, data, 1, start_slope, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MMY, data, 1, start_slope, 1.0f);
                }
                if (a - 2.0f > Mathf.Epsilon)
                { /* a > 2.0f */
                    end_slope = betweenf(a - 2.0f, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PPY, data, 1, 0.0f, end_slope);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MPY, data, 1, 0.0f, end_slope);
                }
                if (a - 3.0f > Mathf.Epsilon)
                { /* a > 3.0f */
                    start_slope = betweenf(4.0f - a, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PPN, data, 1, start_slope, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PMN, data, 1, start_slope, 1.0f);
                }
            }
            if (direction == fov_direction_type.FOV_NORTH)
            {
                end_slope = betweenf(a, 0.0f, 1.0f);
                _fov_octant_part(fov_octants_part.FOV_OCTANT_MPY, data, 1, 0.0f, end_slope);
                _fov_octant_part(fov_octants_part.FOV_OCTANT_MMY, data, 1, 0.0f, end_slope);

                if (a - 1.0f > Mathf.Epsilon)
                { /* a > 1.0f */
                    start_slope = betweenf(2.0f - a, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MMN, data, 1, start_slope, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PMN, data, 1, start_slope, 1.0f);
                }
                if (a - 2.0f > Mathf.Epsilon)
                { /* a > 2.0f */
                    end_slope = betweenf(a - 2.0f, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MPN, data, 1, 0.0f, end_slope);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PPN, data, 1, 0.0f, end_slope);
                }
                if (a - 3.0f > Mathf.Epsilon)
                { /* a > 3.0f */
                    start_slope = betweenf(4.0f - a, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PMY, data, 1, start_slope, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PPY, data, 1, start_slope, 1.0f);
                }
            }
            if (direction == fov_direction_type.FOV_SOUTH)
            {
                end_slope = betweenf(a, 0.0f, 1.0f);
                _fov_octant_part(fov_octants_part.FOV_OCTANT_PMY, data, 1, 0.0f, end_slope);
                _fov_octant_part(fov_octants_part.FOV_OCTANT_PPY, data, 1, 0.0f, end_slope);

                if (a - 1.0f > Mathf.Epsilon)
                { /* a > 1.0f */
                    start_slope = betweenf(2.0f - a, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MPN, data, 1, start_slope, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PPN, data, 1, start_slope, 1.0f);
                }
                if (a - 2.0f > Mathf.Epsilon)
                { /* a > 2.0f */
                    end_slope = betweenf(a - 2.0f, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MMN, data, 1, 0.0f, end_slope);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PMN, data, 1, 0.0f, end_slope);
                }
                if (a - 3.0f > Mathf.Epsilon)
                { /* a > 3.0f */
                    start_slope = betweenf(4.0f - a, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MMY, data, 1, start_slope, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MPY, data, 1, start_slope, 1.0f);
                }
            }



            if (direction == fov_direction_type.FOV_NORTHEAST) 
            {
                start_slope = betweenf(1.0f - a, 0.0f, 1.0f);
                _fov_octant_part(fov_octants_part.FOV_OCTANT_PMN, data, 1, start_slope, 1.0f);
                _fov_octant_part(fov_octants_part.FOV_OCTANT_MPY, data, 1, start_slope, 1.0f);
    
                if (a - 1.0f > Mathf.Epsilon) 
                { /* a > 1.0f */          
                    end_slope = betweenf(a - 1.0f, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MMY, data, 1, 0.0f, end_slope);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PPN, data, 1, 0.0f, end_slope);
                }
                if (a - 2.0f > Mathf.Epsilon) 
                { /* a > 2.0f */          
                    start_slope = betweenf(3.0f - a, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MMN, data, 1, start_slope, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PPY, data, 1, start_slope, 1.0f);
                }
                if (a - 3.0f > Mathf.Epsilon) 
                { /* a > 3.0f */          
                    end_slope = betweenf(a - 3.0f, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MPN, data, 1, 0.0f, end_slope);       
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PMY, data, 1, 0.0f, end_slope);
                }
            }
            if (direction == fov_direction_type.FOV_NORTHWEST)
            {
                start_slope = betweenf(1.0f - a, 0.0f, 1.0f);
                _fov_octant_part(fov_octants_part.FOV_OCTANT_MMN, data, 1, start_slope, 1.0f);
                _fov_octant_part(fov_octants_part.FOV_OCTANT_MMY, data, 1, start_slope, 1.0f);

                if (a - 1.0f > Mathf.Epsilon)
                { /* a > 1.0f */
                    end_slope = betweenf(a - 1.0f, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MPN, data, 1, 0.0f, end_slope);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MPY, data, 1, 0.0f, end_slope);
                }
                if (a - 2.0f > Mathf.Epsilon)
                { /* a > 2.0f */
                    start_slope = betweenf(3.0f - a, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PMY, data, 1, start_slope, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PMN, data, 1, start_slope, 1.0f);
                }
                if (a - 3.0f > Mathf.Epsilon)
                { /* a > 3.0f */
                    end_slope = betweenf(a - 3.0f, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PPY, data, 1, 0.0f, end_slope);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PPN, data, 1, 0.0f, end_slope);
                }
            }
            if (direction == fov_direction_type.FOV_SOUTHEAST)
            {
                start_slope = betweenf(1.0f - a, 0.0f, 1.0f);
                _fov_octant_part(fov_octants_part.FOV_OCTANT_PPN, data, 1, start_slope, 1.0f);
                _fov_octant_part(fov_octants_part.FOV_OCTANT_PPY, data, 1, start_slope, 1.0f);

                if (a - 1.0f > Mathf.Epsilon)
                { /* a > 1.0f */
                    end_slope = betweenf(a - 1.0f, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PMY, data, 1, 0.0f, end_slope);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PMN, data, 1, 0.0f, end_slope);
                }
                if (a - 2.0f > Mathf.Epsilon)
                { /* a > 2.0f */
                    start_slope = betweenf(3.0f - a, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MPN, data, 1, start_slope, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MPY, data, 1, start_slope, 1.0f);
                }
                if (a - 3.0f > Mathf.Epsilon)
                { /* a > 3.0f */
                    end_slope = betweenf(a - 3.0f, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MMN, data, 1, 0.0f, end_slope);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MMY, data, 1, 0.0f, end_slope);
                }
            }
            if (direction == fov_direction_type.FOV_SOUTHWEST)
            {
                start_slope = betweenf(1.0f - a, 0.0f, 1.0f);
                _fov_octant_part(fov_octants_part.FOV_OCTANT_PMY, data, 1, start_slope, 1.0f);
                _fov_octant_part(fov_octants_part.FOV_OCTANT_MPN, data, 1, start_slope, 1.0f);

                if (a - 1.0f > Mathf.Epsilon)
                { /* a > 1.0f */
                    end_slope = betweenf(a - 1.0f, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PPY, data, 1, 0.0f, end_slope);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MMN, data, 1, 0.0f, end_slope);
                }
                if (a - 2.0f > Mathf.Epsilon)
                { /* a > 2.0f */
                    start_slope = betweenf(3.0f - a, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PPN, data, 1, start_slope, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MMY, data, 1, start_slope, 1.0f);
                }
                if (a - 3.0f > Mathf.Epsilon)
                { /* a > 3.0f */
                    end_slope = betweenf(a - 3.0f, 0.0f, 1.0f);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_PMN, data, 1, 0.0f, end_slope);
                    _fov_octant_part(fov_octants_part.FOV_OCTANT_MPY, data, 1, 0.0f, end_slope);
                }
            }
        }






        /* Circular FOV --------------------------------------------------- */


        private static List<uint> precalculate_heights(uint maxdist)
        {
            List<uint> result = new List<uint>();

            for (uint i = 0; i <= maxdist; ++i)
            {
                result.Add( (uint)Mathf.Sqrt((float)(maxdist * maxdist - i * i)) );
            }
            result.Add(0);

            return result;
        }

        private static uint height(fov_settings_type settings, int x, uint maxdist)
        {
            if (maxdist > settings.numheights)
            {
                settings.numheights = maxdist;         
            }

            if (settings.heights != null)
            {
                if (!settings.heights.ContainsKey(maxdist - 1))
                {
                    settings.heights.Add(maxdist - 1, precalculate_heights(maxdist));
                }
                if (settings.heights.ContainsKey(maxdist - 1))
                {
                    List<uint> val = null;
                    settings.heights.TryGetValue(maxdist - 1, out val);
                    return val[Mathf.Abs(x)];
                }
            }
            return 0;
        }

        /* Slope ---------------------------------------------------------- */

        private static float fov_slope(float dx, float dy)
        {
            if (dx <= -Mathf.Epsilon || dx >= Mathf.Epsilon)
            {
                return dy / dx;
            }
            else
            {
                return 0.0f;
            }
        }

        /**
         * Limit x to the range [a, b].
         */
        private static float betweenf(float x, float a, float b)
        {
            if (x - a < Mathf.Epsilon)
            { /* x < a */
                return a;
            }
            else if (x - b > Mathf.Epsilon)
            { /* x > b */
                return b;
            }
            else
            {
                return x;
            }
        }


        /* Octants -------------------------------------------------------- */

        private static void fov_octant( 
            fov_private_data_type data, int dx, float start_slope, float end_slope,
            int signx, int signy, char rx, char ry, bool apply_edge, bool apply_diag)
        {                                      
            int x = 0;
            int y = 0;
            int dy, dy0, dy1;                                                                 
            uint h = 0;                                                                             
            int prev_blocked = -1;                                                                  
            float end_slope_next;                                                                   
            fov_settings_type settings = data.settings;                                           
                                                                                                
            if (dx == 0)
            {
                fov_octant(data, dx + 1, start_slope, end_slope, signx, signy, rx, ry, apply_edge, apply_diag);                        
                return;                                                                             
            } 
            else if ((uint)dx > data.radius) 
            {                                               
                return;                                                                             
            }                                                                                       
                                                                                                
            dy0 = (int)(0.5f + ((float)dx) * start_slope);                                            
            dy1 = (int)(0.5f + ((float)dx) * end_slope);                                              
               
            if (rx == 'x')
                x = data.source_x + signx * dx;
            else if (rx == 'y')
                y = data.source_y + signx * dx;

            if (ry == 'y')
                y = data.source_y + signy * dy0;
            else if (ry == 'x')
                x = data.source_x + signy * dy0;
                                                
                                                                 
                                                                 
                                                                                                
            if (!apply_diag && dy1 == dx) 
            {                                                         
                /* We do diagonal lines on every second octant, so they don't get done twice. */    
                --dy1;                                                                              
            }                                                                                       
                                                                                                
            switch (settings.shape) 
            {                                                              
            case fov_shape_type.FOV_SHAPE_CIRCLE_PRECALCULATE:                                                     
                h = height(settings, dx, data.radius);                                             
                break;                                                                              
            case fov_shape_type.FOV_SHAPE_CIRCLE:                                                                  
                h = (uint)Mathf.Sqrt((float)(data.radius * data.radius - dx * dx));                    
                break;                                                                              
            case fov_shape_type.FOV_SHAPE_OCTAGON:                                                                 
                h = (uint)(data.radius - dx) << 1;                                                         
                break;                                                                              
            default:                                                                                
                h = data.radius;                                                                   
                break;                                                                              
            };         
                                                                             
            if ((uint)dy1 > h)
            {                                                                
                if (h == 0) 
                {                                                                       
                    return;                                                                         
                }                                                                                   
                dy1 = (int)h;                                                                       
            }                                                                                       
                                                                                                
            /*fprintf(stderr, "(%2d) = [%2d .. %2d] (%f .. %f), h=%d,edge=%d\n",                    
                    dx, dy0, dy1, ((float)dx)*start_slope,                                          
                    0.5f + ((float)dx)*end_slope, h, apply_edge);*/                                 
                                                                                                
            for (dy = dy0; dy <= dy1; ++dy) 
            {
                if (ry == 'y')
                    y = data.source_y + signy * dy;
                else if (ry == 'x')
                    x = data.source_x + signy * dy;
                                                                                                
                if (settings.m_OnOpaqueEventHandler(data._map, x, y))
                {                                            
                    if (settings.opaque_apply == fov_opaque_apply_type.FOV_OPAQUE_APPLY && (apply_edge || dy > 0))
                    {     
                        settings.m_OnApplyEventHandler(data._map, x, y, dx, dy, data.source);                     
                    }                                                                               
                    if (prev_blocked == 0)
                    {                                                        
                        end_slope_next = fov_slope((float)dx + 0.5f, (float)dy - 0.5f);
                        fov_octant(data, dx + 1, start_slope, end_slope_next, signx, signy, rx, ry, apply_edge, apply_diag);           
                    }                                                                               
                    prev_blocked = 1;                                                               
                } else
                {                                                                            
                    if (apply_edge || dy > 0) 
                    {                                                     
                        settings.m_OnApplyEventHandler(data._map, x, y, dx, dy, data.source);                     
                    }                                                                               
                    if (prev_blocked == 1) 
                    {                                                        
                        start_slope = fov_slope((float)dx - 0.5f, (float)dy - 0.5f);                
                    }                                                                               
                    prev_blocked = 0;                                                               
                }                                                                                   
            }                                                                                       
                                                                                                
            if (prev_blocked == 0) 
            {
                fov_octant(data, dx + 1, start_slope, end_slope, signx, signy, rx, ry, apply_edge, apply_diag);                        
            }                                                                                       
        }



        /* Circle --------------------------------------------------------- */

        private static void _fov_circle(fov_private_data_type data)
        {
            /*
             * Octants are defined by (x,y,r) where:
             *  x = [p]ositive or [n]egative x increment
             *  y = [p]ositive or [n]egative y increment
             *  r = [y]es or [n]o for reflecting on axis x = y
             *
             *   \pmy|ppy/
             *    \  |  /
             *     \ | /
             *   mpn\|/ppn
             *   ----@----
             *   mmn/|\pmn
             *     / | \
             *    /  |  \
             *   /mmy|mpy\
             */
            fov_octant(data, 1, (float)0.0f, (float)1.0f, +1, +1, 'x', 'y', true, true);          // PPN
            fov_octant(data, 1, (float)0.0f, (float)1.0f, +1, +1, 'y', 'x', true, false);         // PPY
            fov_octant(data, 1, (float)0.0f, (float)1.0f, +1, -1, 'x', 'y', false, true);         // PMN
            fov_octant(data, 1, (float)0.0f, (float)1.0f, +1, -1, 'y', 'x', false, false);        // PMY
            fov_octant(data, 1, (float)0.0f, (float)1.0f, -1, +1, 'x', 'y', true, true);          // MPN
            fov_octant(data, 1, (float)0.0f, (float)1.0f, -1, +1, 'y', 'x', true, false);         // MPY
            fov_octant(data, 1, (float)0.0f, (float)1.0f, -1, -1, 'x', 'y', false, true);         // MMN
            fov_octant(data, 1, (float)0.0f, (float)1.0f, -1, -1, 'y', 'x', false, false);        // MMY
        }





        private static void _fov_octant_part(fov_octants_part part, 
            fov_private_data_type data, int dx, float start_slope, float end_slope)
        {
            switch(part)
            {
                case fov_octants_part.FOV_OCTANT_PPN:
                    fov_octant(data, dx, start_slope, end_slope, +1, +1, 'x', 'y', true, true);    // PPN
                    break;
                case fov_octants_part.FOV_OCTANT_PPY:
                    fov_octant(data, dx, start_slope, end_slope, +1, +1, 'y', 'x', true, false);   // PPY
                    break;
                case fov_octants_part.FOV_OCTANT_PMN:
                    fov_octant(data, dx, start_slope, end_slope, +1, -1, 'x', 'y', false, true);   // PMN
                    break;
                case fov_octants_part.FOV_OCTANT_PMY:
                    fov_octant(data, dx, start_slope, end_slope, +1, -1, 'y', 'x', false, false);  // PMY
                    break;
                case fov_octants_part.FOV_OCTANT_MPN:
                    fov_octant(data, dx, start_slope, end_slope, -1, +1, 'x', 'y', true, true);    // MPN
                    break;
                case fov_octants_part.FOV_OCTANT_MPY:
                    fov_octant(data, dx, start_slope, end_slope, -1, +1, 'y', 'x', true, false);   // MPY
                    break;
                case fov_octants_part.FOV_OCTANT_MMN:
                    fov_octant(data, dx, start_slope, end_slope, -1, -1, 'x', 'y', false, true);   // MMN
                    break;
                case fov_octants_part.FOV_OCTANT_MMY:
                    fov_octant(data, dx, start_slope, end_slope, -1, -1, 'y', 'x', false, false);  // MMY
                    break;
            }
        }



    }
}
