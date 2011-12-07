﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace SeatBattle.CSharp
{
    public class Board : Control
    {
        private const int BoardHeight = 10;
        private const int BoardWidth = 10;

        private readonly BoardCell[,] _cells;
        private readonly Label[] _rowHeaders;
        private readonly Label[] _columnHeaders;
        private readonly List<Ship> _ships;
        private DraggableShip _draggedShip;
        private bool _shipOrientationModified;


        public Board()
        {
            _cells = new BoardCell[10, 10];
            _rowHeaders = new Label[10];
            _columnHeaders = new Label[10];
            _ships = new List<Ship>();

            CreateRowHeaders();
            CreateColumnHeaders();
            CreateBoard();

            base.MinimumSize = new Size(CellSize.Width * 11, CellSize.Height * 11);
            base.MaximumSize = new Size(CellSize.Width * 11, CellSize.Height * 11);
        }

        private void CreateColumnHeaders()
        {
            for (var i = 1; i < 11; i++)
            {
                var label = new Label
                                {
                                    AutoSize = false,
                                    BackColor = BackColor,
                                    TextAlign = ContentAlignment.MiddleCenter,
                                    Text = i.ToString(),
                                    Location = new Point(CellSize.Width * i, 0),
                                    Size = CellSize
                                };
                _columnHeaders[i - 1] = label;
                Controls.Add(label);

            }

        }

        private void CreateRowHeaders()
        {
            for (var i = 1; i < 11; i++)
            {
                var label = new Label
                                {
                                    AutoSize = false,
                                    BackColor = BackColor,
                                    TextAlign = ContentAlignment.MiddleCenter,
                                    Text = i.ToString(),
                                    Location = new Point(0, CellSize.Height * i),
                                    Size = CellSize
                                };
                _rowHeaders[i - 1] = label;
                Controls.Add(label);
            }

        }

        private void CreateBoard()
        {
            for (var x = 0; x < 10; x++)
            {
                for (var y = 0; y < 10; y++)
                {
                    var cell = new BoardCell(x, y)
                                   {
                                       Size = CellSize,
                                       Location = new Point(CellSize.Width * (x + 1), CellSize.Height * (y + 1)),
                                       State = BoardCellState.Normal,
                                       //IsValidForNewShip = true
                                   };
                    _cells[x, y] = cell;
                    cell.MouseDown += OnCellMouseDown;
                    cell.DragEnter += OnCellDragEnter;
                    cell.DragLeave += OnCellDragLeave;
                    cell.DragDrop += OnCellDragDrop;
                    cell.QueryContinueDrag += OnCellQueryContinueDrag;
                    Controls.Add(cell);
                }
            }
        }

        private void OnCellQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) && !_shipOrientationModified)
            {
                var rect = _draggedShip.GetShipRegion();
                RedrawRegion(rect);
                _draggedShip.Rotate();
                _shipOrientationModified = true;
                DrawShip(_draggedShip, BoardCellState.ShipDrag);
            }
            else if (Keyboard.IsKeyUp(Key.LeftCtrl) && _shipOrientationModified)
            {
                var rect = _draggedShip.GetShipRegion();
                RedrawRegion(rect);
                _draggedShip.Rotate();
                _shipOrientationModified = false;
                DrawShip(_draggedShip, BoardCellState.ShipDrag);
            }
        }

        private Ship GetShipAt(int x, int y)
        {
            return _ships.FirstOrDefault(ship => ship.IsLocatedAt(x, y));
        }

        private void OnCellMouseDown(object sender, MouseEventArgs e)
        {
            var cell = (BoardCell)sender;
            var ship = GetShipAt(cell.X, cell.Y);

            if (ship == null)
                return;

            _draggedShip = DraggableShip.From(ship);
            cell.DoDragDrop(ship, DragDropEffects.Copy | DragDropEffects.Move);
        }

        private void OnCellDragEnter(object sender, DragEventArgs e)
        {
            Debug.WriteLine("OnCellDragEnter");
            if (e.Data.GetDataPresent(typeof(Ship)))
            {
                var cell = (BoardCell)sender;
                _draggedShip.MoveTo(cell.X, cell.Y);

                var canPlaceShip = CanPlaceShip(_draggedShip, cell.X, cell.Y);
                var state = canPlaceShip ? BoardCellState.ShipDrag : BoardCellState.ShipDragInvalid;

                DrawShip(_draggedShip, state);

                e.Effect = canPlaceShip ? DragDropEffects.Move : DragDropEffects.None;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void OnCellDragLeave(object sender, EventArgs e)
        {
            Debug.WriteLine("OnCellDragLeave");
            var rect = _draggedShip.GetShipRegion();
            RedrawRegion(rect);

        }

        private void OnCellDragDrop(object sender, DragEventArgs e)
        {
            Debug.WriteLine("OnCellDragDrop");
            var cell = (BoardCell)sender;
            if (e.Data.GetDataPresent(typeof(Ship)))
            {
                if (!CanPlaceShip(_draggedShip, cell.X, cell.Y))
                    return;
                var ship = _draggedShip.Source;
                ship.Orientation = _draggedShip.Orientation;

                MoveShip(ship, cell.X, cell.Y);
                _draggedShip = null;

            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
            cell.Invalidate();
            _shipOrientationModified = false;
        }

        public Size CellSize { get { return new Size(25, 25); } }

        public void AddShip(Ship ship, int x, int y)
        {
            if (!CanPlaceShip(ship, x, y))
                throw new InvalidOperationException("Cannot place ship at a given location");

            ship.Location = new Point(x, y);
            _ships.Add(ship);
            DrawShip(ship, BoardCellState.Ship);
        }

        private bool CanPlaceShip(Ship ship, int x, int y)
        {

            var r = ship.GetShipRegion();

            r.Location = new Point(x, y);

            if (r.Right >= BoardWidth || r.Bottom >= BoardHeight)
                return false;


            r.Inflate(1, 1);



            foreach (var s in _ships)
            {
                if (s.GetShipRegion().IntersectsWith(r))
                    return false;
            }

            return true;
        }

        private void RedrawRegion(Rectangle region)
        {
            SuspendLayout();
            for (var x = region.X; x <= region.Right; x++)
            {
                for (var y = region.Y; y <= region.Bottom; y++)
                {
                    if (x >= BoardWidth || y >= BoardHeight)
                    {
                        continue;
                    }

                    var ship = GetShipAt(x, y);
                    _cells[x, y].State = ship == null ? BoardCellState.Normal : BoardCellState.Ship;
                    _cells[x, y].Invalidate();
                }
            }
            ResumeLayout();
        }

        private void DrawShip(Ship ship, BoardCellState state)
        {
            SuspendLayout();
            var rect = ship.GetShipRegion();

            for (var dx = rect.X; dx <= rect.Width; dx++)
            {
                for (var dy = rect.Y; dy <= rect.Height; dy++)
                {
                    if (dx < BoardWidth && dy < BoardHeight)
                    {
                        var cell = _cells[dx, dy];
                        cell.State = state;
                        cell.Invalidate();
                    }
                }
            }
            ResumeLayout();
        }

        private void MoveShip(Ship ship, int x, int y)
        {
            var rect = ship.GetShipRegion();
            _ships.Remove(ship);
            RedrawRegion(rect);


            //ship.Location = new Point(x, y);
            AddShip(ship, x, y);
        }

        public void ClearBoard()
        {
            SuspendLayout();
            _ships.Clear();
            for (int i = 0; i < BoardWidth; i++)
            {
                for (int j = 0; j < BoardHeight; j++)
                {
                    _cells[i, j].State = BoardCellState.Normal;
                    _cells[i, j].IsOccupied = false;
                }
            }
            ResumeLayout();
        }

        public void AddRandomShips()
        {
            var rnd = new Random(DateTime.Now.Millisecond);
            var ships = new List<Ship>
                        {
                            new Ship(4){Orientation = (ShipOrientation)rnd.Next(2)},
                            new Ship(3){Orientation = (ShipOrientation)rnd.Next(2)},
                            new Ship(3){Orientation = (ShipOrientation)rnd.Next(2)},
                            new Ship(2){Orientation = (ShipOrientation)rnd.Next(2)},
                            new Ship(2){Orientation = (ShipOrientation)rnd.Next(2)},
                            new Ship(2){Orientation = (ShipOrientation)rnd.Next(2)},
                            new Ship(1){Orientation = (ShipOrientation)rnd.Next(2)},
                            new Ship(1){Orientation = (ShipOrientation)rnd.Next(2)},
                            new Ship(1){Orientation = (ShipOrientation)rnd.Next(2)},
                            new Ship(1){Orientation = (ShipOrientation)rnd.Next(2)}
                        };

            var shipsPlaced = 0;


            foreach (var ship in ships)
            {
                var shipPlaced = false;
                var retries = 0;
                while (!shipPlaced && retries < 10)
                {
                    var x = rnd.Next(11);
                    var y = rnd.Next(11);

                    if (CanPlaceShip(ship, x, y))
                    {
                        AddShip(ship, x, y);
                        shipPlaced = true;
                        shipsPlaced++;
                        Refresh();
                        continue;
                    }
                    retries++;
                }
                for (int i = 0; i < BoardWidth; i++)
                {
                    if (shipPlaced)
                        break;

                    for (int j = 0; j < BoardHeight; j++)
                    {
                        if (shipPlaced)
                            break;
                        if (CanPlaceShip(ship, i, j))
                        {
                            AddShip(ship, i, j);
                            shipsPlaced++;
                            shipPlaced = true;
                            Refresh();
                            break;
                            
                        }
                    }
                }
            }

        }
    }
}
