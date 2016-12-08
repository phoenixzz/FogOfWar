using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;

namespace Game.Tools.FOV
{

    class map
    {
        class cell
        {
            public byte tile;
            public bool seen;
            public bool remembered;
        }

        private cell[] cells = new cell[0];
        private int MAP_WIDTH = 0;
        private int MAP_HEIGHT = 0;

        private map() { }
        public map(int width, int height)
        {
            // fill the map with blocking cells and set all cells to not seen
            MAP_WIDTH = width;
            MAP_HEIGHT = height;


            cells = new cell[width * height];
            for (int i=0; i<height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    cell newcell = new cell();
                    newcell.tile = (byte)('.');
                    newcell.seen = false;
                    newcell.remembered = false;
                    cells[i * width + j] = newcell;

                }               
            }
        }

        public void initBlock(int x, int y, bool bBlock)
        {
            if (!onMap(x, y))
                return;

            cells[x + y * MAP_WIDTH].tile = bBlock ? (byte)('#') : (byte)('.');
        }

        public void setSeen(int x, int y)
        {
            if (!onMap(x, y))
                return;

            cells[x + y * MAP_WIDTH].seen = true;
            cells[x + y * MAP_WIDTH].remembered = true;
        }

        public void setUnSeen(int x, int y)
        {
            if (!onMap(x, y))
                return;

            cells[x + y * MAP_WIDTH].seen = false;
        }

        public bool isSeen(int x, int y)
        {
            if (!onMap(x, y))
                return false;
            return cells[x + y * MAP_WIDTH].seen;
        }

        public bool isRemembered(int x, int y)
        {
            if (!onMap(x, y))
                return false;
            return cells[x + y * MAP_WIDTH].remembered;
        }

        public bool onMap(int x, int y)
        {
            return (x < MAP_WIDTH && y < MAP_HEIGHT);
        }

        public bool blockLOS(int x, int y)
        {
	        if (!onMap(x,y))
                return true;

            return (cells[x + y * MAP_WIDTH].tile == (byte)('#'));
        }
        
    }
}
