using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapGenerator : MonoBehaviour {

	enum tile {
		wall = 0,
		floor = 1
	}

	public int width, height, birthLimit, deathLimit, steps;
	public long seed;
	[Min(1)]
	public int wallThresholdSize, floorThresholdSize;
    public bool useRandomSeed, useRandomColor, quickload;
	[Range(0,1)]
	public float r, g, b;
	public Tilemap tilemap;
	public Tile wallN, wallS, wallE, wallW, cornerOuterNE,
		cornerOuterSE, cornerOuterSW, cornerOuterNW, cornerInnerNE,
		cornerInnerSE, cornerInnerSW, cornerInnerNW, cornerInnerNWSE, cornerInnerNESW, background;
	public Tile[] floors;
	[Range(0, 100)]
    public int initFillPercent;
    tile[,] map;
	System.Random psuedoRand;

	struct Coord {
		public int x, y;
		public Coord(int x, int y) {
			this.x = x;
			this.y = y;
		}
	}

    void Start() {
		//GenerateMap();
    }
	
	public void GenerateMap() {
		map = new tile[width,height];
		fillMap();
		doSimulationStep();
		//border
		for (int x = 0; x < width; x++) {
			map[x, 0] = tile.wall;
			map[x, height - 1] = tile.wall;
		}
		for (int y = 0; y < width; y++) {
			map[0, y] = tile.wall;
			map[width - 1, y] = tile.wall;
		}
		processMap();
		wallFirstPass();
		doSimulationStep();
		renderMap();
		Debug.Log("map made");
		Camera.allCameras[0].transform.localPosition = tilemap.CellToLocal(new Vector3Int(width / 2, height / 2, 0));
	}

	void fillMap() {
		if (useRandomSeed)
			seed = System.DateTime.Now.Ticks;
		;
		psuedoRand = new System.Random(seed.GetHashCode());
		if (useRandomColor) {
			r = System.Convert.ToSingle(psuedoRand.NextDouble());
			g = System.Convert.ToSingle(psuedoRand.NextDouble());
			b = System.Convert.ToSingle(psuedoRand.NextDouble());
		}
		for (int i = 0; i < width; i++) {
			for (int j = 0; j < height; j++) {
				map[i, j] = (psuedoRand.Next(0, 100) < initFillPercent) ? tile.wall : tile.floor;
			}
		}
	}

	public void doSimulationStep() {
		bool changed = false;
		tile[,] newMap = map;
		for (int x = 0; x < width; x++) {
			for (int y = 0; y < height; y++) {
				int neighbours = countAliveNeighbours(map, x, y);
				//The new value is based on our simulation rules
				//First, if a cell is alive but has too few neighbours, kill it.
				if (map[x,y] == tile.wall) {
					if (neighbours < deathLimit) {
						newMap[x,y] = tile.floor;
						changed = true;
					} else {
						newMap[x,y] = tile.wall;
					}
				} //Otherwise, if the cell is dead now, check if it has the right number of neighbours to be 'born'
				else {
					if (neighbours > birthLimit) {
						newMap[x,y] = tile.wall;
						changed = true;
					} else {
						newMap[x,y] = tile.floor;
					}
				}
			}
		}
		if(changed) {
			map = newMap;
			doSimulationStep();
		}
	}

	int countAliveNeighbours(tile[,] map, int x, int y) {
		int count = 0;
		for (int i = -1; i < 2; i++) {
			for (int j 
= -1; j < 2; j++) {
				int neighbour_x = x + i;
				int neighbour_y = y + j;
				//if(i == 0 || j == 0) {
					//If we're looking at the middle point
					if (i == 0 && j == 0) {
						//Do nothing
					}
					//In case the index we're looking at it off the edge of the map
					else if (!inMap(neighbour_x, neighbour_y)) {
						count = count + 1;
					}
					//Otherwise, a normal check of the neighbour
					else if (map[neighbour_x,neighbour_y] == tile.wall) {
						count = count + 1;
					}
				//}
			}
		}
		return count;
	}

	bool inMap(int x, int y) {
		return (x >=0 && x < width && y >= 0 && y < height);
	}

	void processMap() {
		List<List<Coord>> wallRegions = getRegions(tile.wall);
		List<Room> survivingRooms = new List<Room>();
		foreach (List<Coord> wallRegion in wallRegions) {
			if (wallRegion.Count <= wallThresholdSize) {
				foreach (Coord tile in wallRegion) {
					map[tile.x, tile.y] = MapGenerator.tile.floor;
				}
			}
		}
		List<List<Coord>> floorRegions = getRegions(tile.floor);
		foreach (List<Coord> floorRegion in floorRegions) {
			if (floorRegion.Count <= floorThresholdSize) {
				foreach (Coord tile in floorRegion) {
					map[tile.x, tile.y] = MapGenerator.tile.wall;
				}
			} else {
				survivingRooms.Add(new Room(floorRegion, map));
			}
		}
		survivingRooms.Sort();
		if (quickload) {
			removeExcessRooms(survivingRooms);
		} else {
			survivingRooms[0].main = true;
			survivingRooms[0].isAccessible = true;
			findClosestRooms(survivingRooms);
		}
	}

	void findClosestRooms(List<Room> allRooms, bool forceAccessible = true) {
		float bestDistance = 0;
		bool possibleConnection = false;
		Coord bestTileA = new Coord();
		Coord bestTileB = new Coord();
		Room bestRoomA = new Room();
		Room bestRoomB = new Room();
		List<Room> connectedRooms = new List<Room>();
		List<Room> newRooms = new List<Room>();
		foreach (Room roomA in allRooms) {
			if (!forceAccessible) {
				possibleConnection = false;
				if (roomA.connectedRooms.Count > 0) {
					continue;
				}
			}
			foreach (Room roomB in allRooms) {
				if (roomA == roomB || roomA.isConnected(roomB) || connectedRooms.Contains(roomB) || connectedRooms.Contains(roomA)) {
					continue;
				}
				for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++) {
					for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++) {
						Coord tileA = roomA.edgeTiles[tileIndexA];
						Coord tileB = roomB.edgeTiles[tileIndexB];
						float dist = Mathf.Pow(tileA.x - tileB.x, 2) + Mathf.Pow(tileA.y - tileB.y, 2);

						if (dist < bestDistance || !possibleConnection) {
							bestDistance = dist;
							possibleConnection = true;
							bestTileA = tileA;
							bestTileB = tileB;
							bestRoomA = roomA;
							bestRoomB = roomB;
						}
					}
				}
			}
			if (possibleConnection) {
				if(!connectedRooms.Contains(bestRoomA))
					connectedRooms.Add(bestRoomA);
				if (!connectedRooms.Contains(bestRoomB))
					connectedRooms.Add(bestRoomB);
			}
			foreach (Room t in connectedRooms) {
				allRooms.Remove(t);
			}
			allRooms.Add(createPassage(bestRoomA, bestRoomB, bestTileA, bestTileB));
			break;
		}
		if (allRooms.Count > 1) {
			findClosestRooms(allRooms);
		}
	}

	void removeExcessRooms(List<Room> rooms) {
		rooms.RemoveAt(0);
		foreach (Room r in rooms) {
			foreach(Coord t in r.tiles) {
				map[t.x, t.y] = tile.wall;
			}
		}
	}

	List<List<Coord>> getRegions(tile tileType) {
		List<List<Coord>> regions = new List<List<Coord>>();
		bool[,] mapFlags = new bool[width, height];
		for (int i = 0; i < width; i++) {
			for (int j = 0; j < height; j++) {
				if (mapFlags[i, j] == false && map[i, j] == tileType) {
					List<Coord> newRegion = getRegionTiles(i, j);
					regions.Add(newRegion);
					foreach (Coord tile in newRegion) {
						mapFlags[tile.x, tile.y] = true;
					}
				}
			}
		}
		return regions;
	}

	List<Coord> getRegionTiles(int startX, int startY) {
		List<Coord> tiles = new List<Coord>();
		bool[,] mapFlags = new bool[width, height];
		tile tileType = map[startX, startY];
		Queue<Coord> queue = new Queue<Coord>();
		queue.Enqueue(new Coord(startX, startY));
		mapFlags[startX, startY] = true;
		while (queue.Count > 0) {
			Coord tile = queue.Dequeue();
			tiles.Add(tile);
			for (int x = tile.x - 1; x <= tile.x + 1; x++) {
				for (int y = tile.y - 1; y <= tile.y + 1; y++) {
					if (inMap(x, y) && (y == tile.y || x == tile.x)) {
						if (!mapFlags[x, y] && map[x, y] == tileType) {
							mapFlags[x, y] = true;
							queue.Enqueue(new Coord(x, y));
						}
					}
				}
			}
		}
		return tiles;
	}

	void wallFirstPass() {
		Vector3Int vec;
		for (int x = 0; x < width; x++) {
			for (int y = 0; y < height; y++) {
				vec = new Vector3Int(x, y, 0);
				if (map[x, y] == tile.wall) {
					wallCheck(vec);
				}
			}
		}
	}

	void wallCheck(Vector3Int v) {
		// has a floor tile above
		if (inMap(v.x, v.y + 1) && map[v.x, v.y + 1] == tile.floor) {
			//has a floor tile to the right
			if (inMap(v.x + 1, v.y) && map[v.x + 1, v.y] == tile.floor) {
				// has a wall tile diagonally
				if ((inMap(v.x + 1, v.y + 1) && map[v.x + 1, v.y + 1] == tile.wall)) {
					map[v.x, v.y] = tile.floor;
				}else if (inMap(v.x, v.y - 1) && map[v.x, v.y - 1] == tile.floor){
					map[v.x, v.y] = tile.floor;
				}
				//has a floor tile to the left
			} else if (inMap(v.x - 1, v.y) && map[v.x - 1, v.y] == tile.floor) {
				// has a wall tile diagonally
				if ((inMap(v.x - 1, v.y + 1) && map[v.x - 1, v.y + 1] == tile.wall)) {
					map[v.x, v.y] = tile.floor;
				}
				//has a floor tile below
			} else if (inMap(v.x, v.y - 1) && map[v.x, v.y - 1] == tile.floor) {
				map[v.x, v.y] = tile.floor;
				//only has a floor tile above
			} else {
				// has a floor tile diagonally up-left & down-right
				if ((inMap(v.x - 1, v.y + 1) && map[v.x - 1, v.y + 1] == tile.floor) && (inMap(v.x + 1, v.y - 1) && map[v.x + 1, v.y - 1] == tile.floor)) {
					map[v.x, v.y + 1] = tile.wall;
					// has a floor tile diagonally up-right & down-left
				} else if ((inMap(v.x + 1, v.y + 1) && map[v.x + 1, v.y + 1] == tile.floor) && (inMap(v.x - 1, v.y - 1) && map[v.x - 1, v.y - 1] == tile.floor)) {
					map[v.x, v.y + 1] = tile.wall;
				}

			}
			// has a floor tile below
		} else if (inMap(v.x + 1, v.y) && map[v.x + 1, v.y] == tile.floor) {
			//has a floor tile to the left
			if (inMap(v.x - 1, v.y) && map[v.x - 1, v.y] == tile.floor) {
				map[v.x, v.y] = tile.floor;
				// has a floor tile diagonally up-left & down-right
			} else if ((inMap(v.x - 1, v.y + 1) && map[v.x - 1, v.y + 1] == tile.floor) && (inMap(v.x + 1, v.y - 1) && map[v.x + 1, v.y - 1] == tile.floor)) {
				map[v.x + 1, v.y] = tile.wall;
				//only has a floor tile to the right
			}
			//has a floor tile to the left
		} else if (inMap(v.x - 1, v.y) && map[v.x - 1, v.y] == tile.floor) {
			//has a floor tile to the right
			if (inMap(v.x + 1, v.y) && map[v.x + 1, v.y] == tile.floor) {
				map[v.x, v.y] = tile.floor;
				// has a floor tile diagonally up-left & down-right
			} else if ((inMap(v.x - 1, v.y + 1) && map[v.x - 1, v.y + 1] == tile.floor) && (inMap(v.x + 1, v.y - 1) && map[v.x + 1, v.y - 1] == tile.floor)) {
				map[v.x - 1, v.y] = tile.wall;
				//only has a floor tile to the left
			}
			// has floor diagonally up-right
		}
	}

	public void renderMap() {
		//Clear the map (ensures we dont overlap)
		int floorRand;
		tilemap.ClearAllTiles();
		Vector3Int vec;
		for (int x = 0; x < width; x++) {
			for (int y = 0; y < height; y++) {
				vec = new Vector3Int(x, y, 0);
				if (map[x, y] == tile.wall) {
					tilemap.SetTile(vec, getWallType(vec));
					//tilemap.SetTile(vec, background);
				} else {
					floorRand = psuedoRand.Next(0, 101);
					if (floorRand > 99)
						tilemap.SetTile(vec, floors[8]);
					else if (floorRand > 90)
						tilemap.SetTile(vec, floors[psuedoRand.Next(1, 8)]);
					else
						tilemap.SetTile(vec, floors[0]);
				}
			}
		}
	}

	Tile getWallType(Vector3Int v) {
		if(map[v.x, v.y] == tile.floor) {
			return floors[0];
		}
		// has a floor tile above
		if (inMap(v.x, v.y + 1) && map[v.x, v.y + 1] == tile.floor) {
			//has a floor tile to the right
			if (inMap(v.x + 1, v.y) && map[v.x + 1, v.y] == tile.floor) {
				return cornerOuterNE;
			//has a floor tile to the left
			} else if (inMap(v.x - 1, v.y) && map[v.x - 1, v.y] == tile.floor) {
				// has a wall tile diagonally
				return cornerOuterNW;
			//only has a floor tile above
			} else {
				return wallS;
				
			}
		// has a floor tile below
		} else if (inMap(v.x, v.y - 1) && map[v.x, v.y - 1] == tile.floor) {
			//has a floor tile to the right
			if (inMap(v.x + 1, v.y) && map[v.x + 1, v.y] == tile.floor) {
				return cornerOuterSE;
				//has a floor tile to the left
			} else if (inMap(v.x - 1, v.y) && map[v.x - 1, v.y] == tile.floor) {
				return cornerOuterSW;
				//only has a floor tile below
			} else {
				return wallN;
			}
		// has a floor tile to the right
		} else if (inMap(v.x + 1, v.y) && map[v.x + 1, v.y] == tile.floor) {
			return wallW;
		//has a floor tile to the left
		} else if (inMap(v.x - 1, v.y) && map[v.x - 1, v.y] == tile.floor) {
			return wallE;
		// has floor diagonally up-right
		} else if (inMap(v.x + 1, v.y + 1) && map[v.x + 1, v.y + 1] == tile.floor) {
			// has floor diagonally down-left
			if (inMap(v.x - 1, v.y - 1) && map[v.x - 1, v.y - 1] == tile.floor) {
				return cornerInnerNWSE;
			}
			else
				return cornerInnerSW;
		// has floor diagonally up-left
		} else if (inMap(v.x - 1, v.y + 1) && map[v.x - 1, v.y + 1] == tile.floor) {
			// has floor diagonally down-right
			if (inMap(v.x + 1, v.y - 1) && map[v.x + 1, v.y - 1] == tile.floor)
				return cornerInnerNESW;
			else
				return cornerInnerSE;
		// has floor diagonally down-right
		} else if (inMap(v.x + 1, v.y - 1) && map[v.x + 1, v.y - 1] == tile.floor) {
			return cornerInnerNW;
		// has floor diagonally down-left
		} else if (inMap(v.x - 1, v.y - 1) && map[v.x - 1, v.y - 1] == tile.floor) {
			return cornerInnerNE;
		} else return background;
	}

	Room ConnectRooms(Room a, Room b) {
		List<Coord> newTiles = new List<Coord>();
		if (a.isAccessible) {
			b.setAccessible();
		} else if (b.isAccessible) {
			a.setAccessible();
		}
		a.connectedRooms.Add(b);
		b.connectedRooms.Add(a);
		foreach (Coord aTile in a.tiles) {
			newTiles.Add(aTile);
		}
		foreach (Coord bTile in b.tiles) {
			newTiles.Add(bTile);
		}
		return new Room(newTiles, map);
	}

	List<Coord> getLine(Coord from, Coord to) {
		List<Coord> line = new List<Coord>();
		int x = from.x;
		int y = from.y;
		int dx = to.x - from.x;
		int dy = to.y - from.y;
		int step = Math.Sign(dx);
		int grdStep = Math.Sign(dy);
		int longest = Mathf.Abs(dx);
		int shortest = Mathf.Abs(dy);
		bool inverted = false;
		if(longest < shortest) {
			inverted = true;
			longest = Mathf.Abs(dy);
			shortest = Mathf.Abs(dx);
			step = Math.Sign(dy);
			grdStep = Math.Sign(dx);
		}
		int grdAcc = longest / 2;
		for(int i = 0; i < longest; i++) {
			line.Add(new Coord(x, y));
			if (inverted) {
				y += step;
			} else {
				x += step;
			}

			grdAcc += shortest;
			if(grdAcc >= longest) {
				if (inverted) {
					x += grdStep;
				} else {
					y += grdStep;
				}
				grdAcc -= longest;
			}
		}
		return line;
	}

	Room createPassage(Room a, Room b, Coord tileA, Coord tileB) {
		//Debug.DrawLine(tilemap.CellToWorld(new Vector3Int(tileA.x, tileA.y, 0)), tilemap.CellToWorld(new Vector3Int(tileB.x, tileB.y, 0)), Color.red, 100);
		List<Coord> line = getLine(tileA, tileB);
		foreach(Coord l in line) {
			drawCircle(l, 1);
			a.tiles.Add(l);
		}
		return ConnectRooms(a, b);
	}

	void drawCircle(Coord c, int r) {
		for(int x  = -r; x <= r; x++) {
			for (int y = -r; y <= r; y++) {
				if(x*x + y*y <= r*r) {
					int realX = c.x + x;
					int realY = c.y + y;
					if(inMap(realX, realY)) {
						map[realX, realY] = tile.floor;
					}
				}
			}
		}
	}

	

	class Room : IComparable<Room>{
		public List<Coord> tiles, edgeTiles;
		public List<Room> connectedRooms;
		public bool isAccessible, main, hasPassage;
		public int roomSize;
		float bestDistance = 0;
		Coord bestTileA = new Coord();

		public Room() {

		}

		public Room(List<Coord> roomTiles, tile[,] map) {
			tiles = roomTiles;
			roomSize = tiles.Count;
			connectedRooms = new List<Room>();
			edgeTiles = new List<Coord>();

			foreach (Coord tile in tiles) {
				for(int x = tile.x - 1; x <= tile.x + 1; x++) {
					for (int y = tile.y - 1; y <= tile.y + 1; y++) {
						if( x == tile.x || y  == tile.y) {
							if(map[x, y] == MapGenerator.tile.wall) {
								edgeTiles.Add(tile);
							}
						}
					}
				}
			}
		}

		public void setAccessible() {
			if (!isAccessible) {
				isAccessible = true;
				foreach( Room connectedRoom in connectedRooms) {
					connectedRoom.setAccessible();
				}
			}
		}

		public static void ConnectRooms(Room a, Room b) {
			List<Coord> newTiles = new List<Coord>();
			if (a.isAccessible) {
				b.setAccessible();
			}else if (b.isAccessible) {
				a.setAccessible();
			}
			a.connectedRooms.Add(b);
			b.connectedRooms.Add(a);
		}

		public bool isConnected(Room other) {
			return connectedRooms.Contains(other);
		}

		public int CompareTo(Room otherRoom) {
			return otherRoom.roomSize.CompareTo(roomSize);
		}
	}
}
