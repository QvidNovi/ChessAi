using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Mime;
using System.Threading;

namespace ChessAi
{
    /**
     * ChessAi program
     * 
     * COSC 3P71 - Project
     *
     * This program allows for a user to play chess with another user, ai, or have two ai's play each other.
     *
     * It uses a minimax alorithm with alpha-beta pruning and a custom heuristic function for the ai.
     */

    class Program
    {
        public static void Main(string[] args)
        {
            new ChessGame();

            //var b = new Board();
            //Player p1 = new Ai(Board.WhiteIndex);
            //Player p2 = new Ai(Board.BlackIndex, p1);
            //var m = new MoveGenerator();
            //var moves = m.GenerateMoves(b);
            //var move = p1.GetMove(b, moves);
            //Console.WriteLine($"{move.Name}, {move.From} -> {move.To}, {move.Promotion}, Eval: {move.Eval}");
        }
    }

    internal class ChessGame
    {
        private Board Board;
        private Player Player1;
        private Player Player2;
        private Player ActivePlayer;
        public static bool EnableDebugging;
        public static bool EnableHelp;
        private const string StateFileName = "StateFile";
        public static StreamWriter StateFile;

        private Result GameResult;

        public ChessGame(int fileNum = 0)
        {
            while (true)
            {
                try
                {
                    Console.WriteLine("Enable debugging: (y = yes, otherwise no)");
                    var input = Console.ReadLine();
                    EnableDebugging = !string.IsNullOrEmpty(input) && input.Contains("y");
                    if (EnableDebugging)
                    {
                        Console.WriteLine("Debugging enabled: logs will include debugging stats");
                        Console.WriteLine("You may enter 'debug [p,k,b,r,q,k]' to see piece tables (eg: 'debug p' to see pawn tables) during a move");
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.WriteLine("Enable help (board eval/checks/possible moves): (y = yes, otherwise no)");
                        input = Console.ReadLine();
                        EnableHelp = !string.IsNullOrEmpty(input) && input.Contains("y");
                    }

                    Player1 = null;
                    Player2 = null;
                    ActivePlayer = null;

                    Console.WriteLine("How many human players?");
                    var input1 = Console.ReadLine();
                    var numPlayers = 2;
                    var numAi = 0;

                    Console.WriteLine("Is player1 white (0) or black (1)?");
                    var input2 = Console.ReadLine();
                    var player1ColourIndex = Board.WhiteIndex;
                    if (!string.IsNullOrEmpty(input2))
                    {
                        player1ColourIndex = int.Parse(input2) == 1 ? 1 : 0;
                    }

                    int result;
                    if (!int.TryParse(input1, out result))
                    {
                        result = 2;
                    }

                    switch (result)
                    {
                        case 0:
                            numPlayers = 0;
                            numAi = 2;
                            Player1 = new Ai(player1ColourIndex);
                            Player2 = new Ai(1 - player1ColourIndex, Player1);
                            break;
                        case 1:
                            numPlayers = 1;
                            numAi = 1;
                            Player1 = new Player(player1ColourIndex);
                            Player2 = new Ai(1 - player1ColourIndex, Player1);
                            break;
                        case 2:
                            numPlayers = 2;
                            numAi = 0;
                            Player1 = new Player(player1ColourIndex);
                            Player2 = new Player(1 - player1ColourIndex, Player1);
                            break;
                    }

                    if (Player1 == null || Player2 == null)
                    {
                        Player1 = new Player(player1ColourIndex);
                        Player2 = new Player(1 - player1ColourIndex, Player1);
                    }

                    Console.WriteLine($"Starting position (FEN) (empty for default, {Board.StartPosition}):");
                    var input3 = Console.ReadLine();
                    Board = string.IsNullOrEmpty(input3) ? new Board() : new Board(input3);
                    ActivePlayer = player1ColourIndex == (Board.Fen.IsWhiteToMove ? Board.WhiteIndex : Board.BlackIndex)
                        ? Player1
                        : Player2;
                    GameResult = Result.Playing;

                    var fileName = $".\\{StateFileName}{fileNum}.txt";
                    while (File.Exists(fileName))
                    {
                        fileNum++;
                        fileName = $".\\{StateFileName}{fileNum}.txt";
                    }

                    StateFile = new StreamWriter(fileName);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to parse input for game setup");
                    Console.WriteLine(e.Message);
                    Console.ReadKey();
                }

                Run();
                StateFile.Dispose();
                Console.WriteLine("Press any key to continue.");
                Console.ReadKey();
                Console.Clear();
                Console.WriteLine("New Game");
            }
        }

        public void Run()
        {
            MoveGenerator m = new MoveGenerator();
            var moves = m.GenerateMoves(Board);

            do
            {
                if (EnableDebugging || EnableHelp)
                    Board.Print(attackMap:m.OpponentAttackMap);
                else
                    Board.Print();

                WriteStateToFile(Board, GameResult);

                var move = ActivePlayer.GetMove(Board, moves);
                if (move.Quit)
                    return;
                if (move.Undo)
                {
                    if (move.Eval > 0)
                    {
                        Board.UndoMove(move.Eval);
                        if (move.Eval % 2 == 0)
                        {
                            ActivePlayer = ActivePlayer.Opponent;
                        }
                    }
                    else
                    {
                        Board.UndoMove();
                    }
                }
                else
                {
                    if (!moves.Exists(x => x.From == move.From &&
                                           x.To == move.To &&
                                           x.Promotion == move.Promotion))
                    {
                        if (EnableDebugging)
                        {
                            StateFile.WriteLine($"Player returned invalid move: {move.Name}, {move.From} -> {move.To}, {move.Promotion}");
                            Console.WriteLine($"Player returned invalid move: {move.Name}, {move.From} -> {move.To}, {move.Promotion}");
                            Console.WriteLine("Game will end from fatal error, press any key to continue.");
                            Console.ReadKey();
                            return;
                        }
                    }

                    Board.MakeMove(move);
                }

                ActivePlayer = ActivePlayer.Opponent;
                m = new MoveGenerator();
                moves = m.GenerateMoves(Board);
                GameResult = GameState(m, moves);
            } while (GameResult == Result.Playing);

            Board.Print();
            Console.WriteLine(GameResult);
        }

        private void WriteStateToFile(Board board, Result gameResult)
        {
            StateFile.WriteLine();
            StateFile.WriteLine("--------------------------------");
            StateFile.WriteLine($"Current Fen: {board.Fen.ToString()}");

            var bEval = new BoardEval();
            var eval = bEval.Eval(board);
            StateFile.WriteLine($"Board Value: {eval * (Board.Fen.IsWhiteToMove ? 1 : -1)}");
            StateFile.WriteLine($"CapturedWhitePieces: {string.Join(string.Empty, Board.CapturedWhitePieces)}");
            StateFile.WriteLine($"CapturedBlackPieces: {string.Join(string.Empty, Board.CapturedBlackPieces)}");
            StateFile.WriteLine($"MoveList: {string.Join(", ", Board.MoveList)}");
            StateFile.WriteLine("    a  b  c  d  e  f  g  h    ");

            for (var rank = 7; rank >= 0; rank--)
            {
                StateFile.Write(" " + (rank + 1) + " ");

                for (var file = 0; file < 8; file++)
                {
                    var lastMoveFrom = false;
                    var lastMoveTo = false;
                    if (Board.Moves.Count > 0)
                    {
                        lastMoveFrom = Board.CoordFromIndex(board.Moves.Peek().From).Item2 == rank &&
                                       Board.CoordFromIndex(board.Moves.Peek().From).Item1 == file;
                        lastMoveTo = Board.CoordFromIndex(board.Moves.Peek().To).Item2 == rank &&
                                     Board.CoordFromIndex(board.Moves.Peek().To).Item1 == file;
                    }

                    var isWhitePiece = Piece.GetColour(Board.BoardArray[(rank * 8) + file]) == Piece.whiteMask;
                    var isBlackPiece = Piece.GetColour(Board.BoardArray[(rank * 8) + file]) == Piece.blackMask;

                    var symbol = Piece.SymbolFromPiece[Piece.GetType(Board.BoardArray[(rank * 8) + file])];
                    if (isWhitePiece)
                    {
                        symbol = char.ToUpper(symbol);
                    }
                    else if (isBlackPiece)
                    {
                        symbol = char.ToLower(symbol);
                    }
                    else
                    {
                        symbol = '-';
                    }

                    StateFile.Write(" " + symbol + " ");
                }

                StateFile.WriteLine(" " + (rank + 1) + " ");
            }

            StateFile.WriteLine("    a  b  c  d  e  f  g  h    ");
            StateFile.WriteLine();
            StateFile.WriteLine(gameResult);
            StateFile.WriteLine("--------------------------------");
            StateFile.WriteLine();
        }

        private Result GameState(MoveGenerator m, List<Move> moves)
        {
            // Checkmate / Stalemate
            if (moves.Count == 0)
            {
                if (m.InCheck)
                {
                    return (Board.Fen.IsWhiteToMove)
                        ? Result.WhiteIsCheckMated
                        : Result.BlackIsCheckMated;
                }

                return Result.Stalemate;
            }

            // 50-move rule: not implemented.

            // Repetition: not implemented.

            // Insufficient material: not implemented.

            return Result.Playing;
        }

        private enum Result
        {
            Playing,
            WhiteIsCheckMated,
            BlackIsCheckMated,
            Stalemate,
            Repetition,
            FiftyMoveRule,
            InsufficientMaterial,
        }
    }

    internal class Board
    {
        public int[] BoardArray;
        public int[] Kings;
        public PieceList[] Pawns;
        public PieceList[] Knights;
        public PieceList[] Bishops;
        public PieceList[] Rooks;
        public PieceList[] Queens;
        public PieceList[] AllPieces;

        public int ColourIndex;
        public int OpponentColourIndex;
        public int Colour;
        public int OpponentColour;

        public ulong ZobristKey;
        public Stack<ulong> ZobristHistory;

        public const int WhiteIndex = 0;
        public const int BlackIndex = 1;

        public Fen Fen;
        public Stack<Fen> FenHistory;
        public List<char> CapturedWhitePieces;
        public List<char> CapturedBlackPieces;
        public Stack<Move> Moves;
        public List<string> MoveList;
        public Stack<bool> Captures;

        public const string Files = "abcdefgh";
        public const string Ranks = "12345678";
        public const string StartPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        #region Statics

        public static int RankIndex(int index)
        {
            return index >> 3;
        }

        public static int FileIndex(int index)
        {
            return index & 0b000111;
        }

        public static Tuple<int, int> CoordFromIndex(int index)
        {
            return new Tuple<int, int>(FileIndex(index), RankIndex(index));
        }

        public static int IndexFromCoord(int fileIndex, int rankIndex)
        {
            return rankIndex * 8 + fileIndex;
        }

        public static bool LightSquare(int fileIndex, int rankIndex)
        {
            return (fileIndex + rankIndex) % 2 != 0;
        }

        public static string NameFromCoordinate(int fileIndex, int rankIndex)
        {
            return Files[fileIndex] + "" + (rankIndex + 1);
        }

        public PieceList GetPieces(int pieceType, int colourIndex)
        {
            return AllPieces[colourIndex * 8 + pieceType];
        }

        #endregion

        public Board(string fen = StartPosition)
        {
            BoardArray = new int[64];
            Kings = new int[2];

            Fen = new Fen(fen);
            FenHistory = new Stack<Fen>();
            Captures = new Stack<bool>();
            ZobristKey = 0;
            ZobristHistory = new Stack<ulong>();
            FenHistory.Push(Fen);
            LoadBoard();

            Moves = new Stack<Move>();
            MoveList = new List<string>();
            CapturedWhitePieces = new List<char>();
            CapturedBlackPieces = new List<char>();
        }

        public void Print(List<Move> moves = null, ulong attackMap = 0)
        {
            Console.Clear();

            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"Current Fen: {Fen.ToString()}");
            if (ChessGame.EnableHelp || ChessGame.EnableDebugging)
            {
                var bEval = new BoardEval();
                var eval = bEval.Eval(this);
                Console.WriteLine($"Board Value: {eval * (Fen.IsWhiteToMove ? 1 : -1)}");
            }
            Console.WriteLine($"CapturedWhitePieces: {string.Join(string.Empty, CapturedWhitePieces)}");
            Console.WriteLine($"CapturedBlackPieces: {string.Join(string.Empty, CapturedBlackPieces)}");
            Console.WriteLine($"Moves: {string.Join(", ", MoveList)}");
            Console.WriteLine("    a  b  c  d  e  f  g  h    ");

            for (var rank = 7; rank >= 0; rank--)
            {
                Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(" " + (rank + 1) + " ");

                for (var file = 0; file < 8; file++)
                {
                    var lastMoveFrom = false;
                    var lastMoveTo = false;
                    if (Moves.Count > 0)
                    {
                        lastMoveFrom = CoordFromIndex(Moves.Peek().From).Item2 == rank &&
                                       CoordFromIndex(Moves.Peek().From).Item1 == file;
                        lastMoveTo = CoordFromIndex(Moves.Peek().To).Item2 == rank &&
                                     CoordFromIndex(Moves.Peek().To).Item1 == file;
                    }

                    var check = false;
                    if (attackMap != 0)
                    {
                        check = MoveGenerator.ContainsIndex(attackMap, rank * 8 + file);
                    }

                    if (check && lastMoveFrom ^ lastMoveTo)
                    {
                        Console.BackgroundColor = ConsoleColor.Red;
                    }
                    else if (check)
                    {
                        Console.BackgroundColor = ConsoleColor.DarkRed;
                    }
                    else if (lastMoveFrom ^ lastMoveTo)
                    {
                        Console.BackgroundColor = ConsoleColor.Yellow;
                    }
                    else
                    {
                        var isWhiteSquare = (file + rank) % 2 != 0;
                        Console.BackgroundColor = isWhiteSquare ? ConsoleColor.Gray : ConsoleColor.DarkGray;
                    }

                    var isWhitePiece = Piece.GetColour(BoardArray[(rank * 8) + file]) == Piece.whiteMask;
                    var isBlackPiece = Piece.GetColour(BoardArray[(rank * 8) + file]) == Piece.blackMask;

                    if (isWhitePiece)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                    }
                    else if (isBlackPiece)
                    {
                        Console.ForegroundColor = ConsoleColor.Black;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Black;
                    }

                    var symbol = Piece.SymbolFromPiece[Piece.GetType(BoardArray[(rank * 8) + file])];

                    Console.Write(" " + symbol + " ");
                }

                Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(" " + (rank + 1) + " ");
            }

            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("    a  b  c  d  e  f  g  h    ");

            if (moves != null && (ChessGame.EnableDebugging || ChessGame.EnableHelp))
            {
                foreach (var move in moves)
                {
                    Console.Write(
                        $"{Piece.SymbolFromPiece[BoardArray[move.From] & Piece.typeMask]}: ({move.Name}:{move.Eval}), ");
                }
            }
        }

        public void LoadBoard()
        {
            BoardArray = new int[64];
            Pawns = new PieceList[] { new PieceList(8), new PieceList(8) };
            Knights = new PieceList[] { new PieceList(10), new PieceList(10) };
            Bishops = new PieceList[] { new PieceList(10), new PieceList(10) };
            Rooks = new PieceList[] { new PieceList(10), new PieceList(10) };
            Queens = new PieceList[] { new PieceList(9), new PieceList(9) };
            var emptyList = new PieceList();
            AllPieces = new PieceList[]
            {
                emptyList,
                emptyList,
                Pawns[WhiteIndex],
                Knights[WhiteIndex],
                emptyList,
                Bishops[WhiteIndex],
                Rooks[WhiteIndex],
                Queens[WhiteIndex],
                emptyList,
                emptyList,
                Pawns[BlackIndex],
                Knights[BlackIndex],
                emptyList,
                Bishops[BlackIndex],
                Rooks[BlackIndex],
                Queens[BlackIndex],
            };

            int file = 0;
            int rank = 7;

            foreach (var s in Fen.Pieces)
            {
                if (s == '/')
                {
                    file = 0;
                    rank--;
                }
                else
                {
                    if (char.IsDigit(s))
                    {
                        file += (int)char.GetNumericValue(s);
                    }
                    else
                    {
                        var colour = char.IsUpper(s) ? Piece.White : Piece.Black;
                        var colourIndex = char.IsUpper(s) ? WhiteIndex : BlackIndex;
                        var type = Piece.PieceFromSymbol[char.ToLower(s)];

                        BoardArray[IndexFromCoord(file, rank)] = type | colour;

                        if (type == Piece.King)
                        {
                            Kings[colourIndex] = IndexFromCoord(file, rank);
                        }
                        else
                        {
                            GetPieces(type, colourIndex).AddPiece(IndexFromCoord(file, rank));
                        }

                        file++;
                    }
                }
            }

            ColourIndex = Fen.IsWhiteToMove ? WhiteIndex : BlackIndex;
            OpponentColourIndex = 1 - ColourIndex;
            Colour = Fen.IsWhiteToMove ? Piece.White : Piece.Black;
            OpponentColour = !Fen.IsWhiteToMove ? Piece.White : Piece.Black;

            ZobristKey = Zobrist.CalcZobristKey(this);
        }

        public void UpdateFen()
        {
            Fen.Pieces = string.Empty;

            for (var rank = 7; rank >= 0; rank--)
            {
                var emptySquares = 0;

                for (var file = 0; file <= 7; file++)
                {
                    var piece = BoardArray[IndexFromCoord(file, rank)];
                    if ((piece & Piece.typeMask) == Piece.Empty)
                    {
                        emptySquares++;
                    }
                    else
                    {
                        if (emptySquares > 0)
                        {
                            Fen.Pieces += emptySquares;
                        }

                        var symbol = Piece.SymbolFromPiece[piece & Piece.typeMask];
                        Fen.Pieces += (piece & Piece.colourMask) == Piece.whiteMask
                            ? char.ToUpper(symbol)
                            : char.ToLower(symbol);

                        emptySquares = 0;
                    }
                }

                if (emptySquares > 0)
                {
                    Fen.Pieces += emptySquares;
                }

                if (rank != 0)
                {
                    Fen.Pieces += "/";
                }
            }
        }

        public void MakeMove(Move move)
        {
            FenHistory.Push(Fen);

            Fen.FullMoves += Fen.IsWhiteToMove ? 0 : 1;

            var oldEnPassant = Fen.EnPassant;
            Fen.EnPassant = "-";

            try
            {
                // Pieces involved.
                var movedPiece = BoardArray[move.From];
                var movedPieceType = BoardArray[move.From] & Piece.typeMask;
                var capturedPieceType = BoardArray[move.To] & Piece.typeMask;

                // Captures.
                if (capturedPieceType != 0 && oldEnPassant == "-")
                {
                    ZobristKey ^= Zobrist.PiecesArray[capturedPieceType, OpponentColourIndex, move.To];
                    GetPieces(capturedPieceType, OpponentColourIndex).DelPiece(move.To);
                }

                // King moves.
                if (movedPieceType == Piece.King)
                {
                    Kings[ColourIndex] = move.To;

                    // Remove castling rights.
                    Fen.CastlingRights = Fen.CastlingRights.Replace((ColourIndex == WhiteIndex ? "K" : "k"), string.Empty);
                    Fen.CastlingRights = Fen.CastlingRights.Replace((ColourIndex == WhiteIndex ? "Q" : "q"), string.Empty);
                }
                else
                {
                    // Move the piece.
                    GetPieces(movedPieceType, ColourIndex).MovePiece(move.From, move.To);
                }

                var targetPiece = movedPiece;

                // Pawns.
                if ((movedPiece & Piece.typeMask) == Piece.Pawn)
                {
                    // Promotion
                    if (move.Promotion != Piece.Empty)
                    {
                        GetPieces(move.Promotion & Piece.typeMask, ColourIndex).AddPiece(move.To);
                        targetPiece = move.Promotion;
                        Pawns[ColourIndex].DelPiece(move.To);
                        if (ChessGame.EnableDebugging)
                            ChessGame.StateFile.WriteLine($"{((move.Promotion & Piece.colourMask) == Piece.White ? "White" : "Black")} promotes to a {Piece.SymbolFromPiece[move.Promotion & Piece.typeMask]}");
                    }

                    // En Passant
                    else if (NameFromCoordinate(FileIndex(move.To), RankIndex(move.To)) == oldEnPassant)
                    {
                        var ep = move.To + (ColourIndex == WhiteIndex ? -8 : 8);
                        capturedPieceType = Piece.Pawn;
                        BoardArray[ep] = Piece.Empty;
                        Pawns[OpponentColourIndex].DelPiece(ep);
                        ZobristKey ^= Zobrist.PiecesArray[Piece.Pawn, OpponentColourIndex, ep];
                        if (ChessGame.EnableDebugging)
                            ChessGame.StateFile.WriteLine($"{(Fen.IsWhiteToMove ? "White" : "Black")}, captures en passant to {NameFromCoordinate(FileIndex(move.To), RankIndex(move.To))}");
                    }
                }

                // Castling - deal with rooks.
                else if (movedPieceType == Piece.King)
                {
                    if (move.Name == "e1 g1" || move.Name == "e1 h1")
                    {
                        BoardArray[7] = Piece.Empty;
                        BoardArray[5] = Piece.whiteMask | Piece.Rook;
                        Rooks[ColourIndex].MovePiece(7, 5);
                        ZobristKey ^= Zobrist.PiecesArray[Piece.Rook, ColourIndex, 5];
                        ZobristKey ^= Zobrist.PiecesArray[Piece.Rook, ColourIndex, 7];
                        ZobristKey ^= Zobrist.CastlingRights[0b1000];
                        if (ChessGame.EnableDebugging)
                            ChessGame.StateFile.WriteLine($"White castles king side");
                    }
                    else if (move.Name == "e1 c1" || move.Name == "e1 a1")
                    {
                        BoardArray[0] = Piece.Empty;
                        BoardArray[3] = Piece.whiteMask | Piece.Rook;
                        Rooks[ColourIndex].MovePiece(0, 3);
                        ZobristKey ^= Zobrist.PiecesArray[Piece.Rook, ColourIndex, 0];
                        ZobristKey ^= Zobrist.PiecesArray[Piece.Rook, ColourIndex, 3];
                        ZobristKey ^= Zobrist.CastlingRights[0b100];
                        if (ChessGame.EnableDebugging)
                            ChessGame.StateFile.WriteLine($"White castles queen side");
                    }
                    else if (move.Name == "e8 g8" || move.Name == "e8 h8")
                    {
                        BoardArray[63] = Piece.Empty;
                        BoardArray[61] = Piece.blackMask | Piece.Rook;
                        Rooks[ColourIndex].MovePiece(63, 61);
                        ZobristKey ^= Zobrist.PiecesArray[Piece.Rook, ColourIndex, 63];
                        ZobristKey ^= Zobrist.PiecesArray[Piece.Rook, ColourIndex, 61];
                        ZobristKey ^= Zobrist.CastlingRights[0b10];
                        if (ChessGame.EnableDebugging)
                            ChessGame.StateFile.WriteLine($"Black castles king side");
                    }
                    else if (move.Name == "e8 c8" || move.Name == "e8 a8")
                    {
                        BoardArray[56] = Piece.Empty;
                        BoardArray[59] = Piece.blackMask | Piece.Rook;
                        Rooks[ColourIndex].MovePiece(56, 59);
                        ZobristKey ^= Zobrist.PiecesArray[Piece.Rook, ColourIndex, 56];
                        ZobristKey ^= Zobrist.PiecesArray[Piece.Rook, ColourIndex, 59];
                        ZobristKey ^= Zobrist.CastlingRights[0b1];
                        if (ChessGame.EnableDebugging)
                            ChessGame.StateFile.WriteLine($"Black castles queen side");
                    }
                }

                // Move the piece on the board.
                BoardArray[move.To] = targetPiece;
                BoardArray[move.From] = Piece.Empty;

                // Double pawn move and new ep.
                if (movedPieceType == Piece.Pawn &&
                    (Math.Abs(RankIndex(move.From) - RankIndex(move.To)) == 2))
                {
                    var file = FileIndex(move.From) + 1;
                    Fen.EnPassant = NameFromCoordinate(FileIndex(move.From),
                        RankIndex(move.From) + (ColourIndex == WhiteIndex ? 1 : -1));
                    ZobristKey ^= Zobrist.EpFile[file];
                }

                // Handle castling rights.
                if (Fen.CastlingRights.Contains("K"))
                {
                    if (move.From == IndexFromCoord(7, 0) || move.To == IndexFromCoord(7, 0))
                    {
                        Fen.CastlingRights = Fen.CastlingRights.Replace("K", string.Empty);
                        ZobristKey ^= Zobrist.CastlingRights[0b1000];
                    }
                }
                if (Fen.CastlingRights.Contains('Q'))
                {
                    if (move.From == IndexFromCoord(0, 0) || move.To == IndexFromCoord(0, 0))
                    {
                        Fen.CastlingRights = Fen.CastlingRights.Replace("Q", string.Empty);
                        ZobristKey ^= Zobrist.CastlingRights[0b100];
                    }
                }
                if (Fen.CastlingRights.Contains('k'))
                {
                    if (move.From == IndexFromCoord(7, 7) || move.To == IndexFromCoord(7, 7))
                    {
                        Fen.CastlingRights = Fen.CastlingRights.Replace("k", string.Empty);
                        ZobristKey ^= Zobrist.CastlingRights[0b10];
                    }
                }
                if (Fen.CastlingRights.Contains('q'))
                {
                    if (move.From == IndexFromCoord(0, 7) || move.To == IndexFromCoord(0, 7))
                    {
                        Fen.CastlingRights = Fen.CastlingRights.Replace("q", string.Empty);
                        ZobristKey ^= Zobrist.CastlingRights[0b1];
                    }
                }

                if (Fen.CastlingRights == string.Empty)
                {
                    Fen.CastlingRights = "-";
                }

                ZobristKey ^= Zobrist.ColourToMove;
                ZobristKey ^= Zobrist.PiecesArray[movedPieceType, ColourIndex, move.From];
                ZobristKey ^= Zobrist.PiecesArray[BoardArray[move.To] & Piece.typeMask, ColourIndex, move.To];

                if (oldEnPassant != "-")
                {
                    ZobristKey ^= Zobrist.EpFile[Files.IndexOf(oldEnPassant[0]) + 1];
                }

                MoveList.Add(move.Name);
                Captures.Push(capturedPieceType != Piece.Empty);

                if (Captures.Peek() || movedPieceType == Piece.Pawn)
                {
                    Fen.HalfMoves = 0;
                }
                else
                {
                    Fen.HalfMoves++;
                }

                if (Captures.Peek())
                {
                    if (!Fen.IsWhiteToMove)
                    {
                        CapturedWhitePieces.Add(Piece.SymbolFromPiece[capturedPieceType]);
                    }
                    else
                    {
                        CapturedBlackPieces.Add(Piece.SymbolFromPiece[capturedPieceType]);
                    }
                }

                Fen.IsWhiteToMove = !Fen.IsWhiteToMove;
                Colour = (Fen.IsWhiteToMove) ? Piece.White : Piece.Black;
                OpponentColour = (Fen.IsWhiteToMove) ? Piece.Black : Piece.White;
                ColourIndex = 1 - ColourIndex;
                OpponentColourIndex = 1 - ColourIndex;

                Moves.Push(move);
                ZobristHistory.Push(ZobristKey);

                UpdateFen();
            }
            catch (Exception e)
            {
                // Parse failed.
                if (ChessGame.EnableDebugging)
                {
                    ChessGame.StateFile.WriteLine($"Move failed: {e.Message}");
                }
            }
        }

        public void UndoMove(int numToUndo = 1)
        {
            try
            {
                if (numToUndo <= 0)
                {
                    Console.WriteLine("Nothing to undo");
                    Console.ReadKey();
                    return;
                }

                for (int i = 0; i < numToUndo; i++)
                {
                    Fen = FenHistory.Pop();
                    Moves.Pop();
                    MoveList.RemoveAt(MoveList.Count - 1);

                    if (Captures.Pop())
                    {
                        if (Fen.IsWhiteToMove)
                        {
                            CapturedBlackPieces.RemoveAt(CapturedBlackPieces.Count - 1);
                        }
                        else
                        {
                            CapturedWhitePieces.RemoveAt(CapturedWhitePieces.Count - 1);
                        }
                    }

                    ZobristKey = ZobristHistory.Pop();
                }

                LoadBoard();
            }
            catch (Exception e)
            {
                if (ChessGame.EnableDebugging)
                {
                    ChessGame.StateFile.WriteLine("Undo move failed");
                    ChessGame.StateFile.WriteLine(e.Message);
                }
            }
        }
    }

    internal class MoveGenerator
    {
        /**
         * INFO:
         * This internal class is an altered version from Sebastian Lague's Chess-AI Project
         * which can be found here: https://github.com/SebLague/Chess-AI/
         * Adjustments were made to fit into this program, license permitting.
         */
        List<Move> Moves = new List<Move>();

        private bool IsWhiteToMove;

        public bool InCheck = false;
        private bool InDoubleCheck = false;
        private bool PinsExist = false;
        private ulong CheckRayMask = 0;
        private ulong PinRayMask = 0;

        private ulong OpponentKnightAttacks;
        private ulong OpponentAttackMapNoPawns;
        private ulong OpponentSlidingAttackMap;

        public ulong OpponentAttackMap;
        public ulong OpponentPawnAttackMap;

        private bool DoQuietMoves;
        private Board Board;

        public void Initialize()
        {
            Moves = new List<Move>(64);

            InCheck = false;
            InDoubleCheck = false;
            PinsExist = false;
            CheckRayMask = 0;
            PinRayMask = 0;
        }

        public List<Move> GenerateMoves(Board board, bool doQuietMoves = true)
        {
            Board = board;
            DoQuietMoves = doQuietMoves;

            IsWhiteToMove = Board.Fen.IsWhiteToMove;
            Initialize();

            CalculateAttacks();
            GenKingMoves();

            if (InDoubleCheck)
            {
                return Moves;
            }

            GenSlidingMoves();
            GenKnightMoves();
            GenPawnMoves();

            if (Moves.Count == 0)
            {
                if (ChessGame.EnableDebugging)
                    ChessGame.StateFile.WriteLine("No moves found");
            }

            return Moves;
        }

        private void CalculateAttacks()
        {
            // Sliding attack map
            OpponentSlidingAttackMap = 0;

            PieceList opponentRooks = Board.Rooks[Board.OpponentColourIndex];
            for (var i = 0; i < opponentRooks.Count; i++)
            {
                UpdateSlidingAttack(opponentRooks[i], 0, 4);
            }

            PieceList opponentBishops = Board.Bishops[Board.OpponentColourIndex];
            for (var i = 0; i < opponentBishops.Count; i++)
            {
                UpdateSlidingAttack(opponentBishops[i], 4, 8);
            }

            PieceList opponentQueens = Board.Queens[Board.OpponentColourIndex];
            for (var i = 0; i < opponentQueens.Count; i++)
            {
                UpdateSlidingAttack(opponentQueens[i], 0, 8);
            }

            // Search for pins/checks by enemy sliding pieces.
            var startDir = 0;
            var endDir = 8;

            if (Board.Queens[Board.OpponentColourIndex].Count == 0)
            {
                startDir = (Board.Rooks[Board.OpponentColourIndex].Count > 0) ? 0 : 4;
                endDir = (Board.Bishops[Board.OpponentColourIndex].Count > 0) ? 8 : 4;
            }

            for (var dir = startDir; dir < endDir; dir++)
            {
                bool isDiagonal = dir > 3;

                var n = PrecomputedMoveData.NumSquaresToEdge[Board.Kings[Board.ColourIndex]][dir];
                var dirOffset = PrecomputedMoveData.DirectionOffsets[dir];
                var friendlyAlongRay = false;
                ulong rayMask = 0;

                for (int i = 0; i < n; i++)
                {
                    var index = Board.Kings[Board.ColourIndex] + dirOffset * (i + 1);
                    rayMask |= 1ul << index;
                    var piece = Board.BoardArray[index];

                    // Piece here.
                    if (piece != Piece.Empty)
                    {
                        // Friendly piece.
                        if (Piece.IsColour(piece, Board.Colour))
                        {
                            // First friendly piece, possible pin.
                            if (!friendlyAlongRay)
                            {
                                friendlyAlongRay = true;
                            }
                            // Second friendly piece, no pin.
                            else
                            {
                                break;
                            }
                        }
                        // Opponent piece.
                        else
                        {
                            // Can opponent piece move this way
                            if (isDiagonal && Piece.IsBorQ(piece & Piece.typeMask) ||
                                !isDiagonal && Piece.IsRorQ(piece & Piece.typeMask))
                            {
                                // Friendly blocks, so pin.
                                if (friendlyAlongRay)
                                {
                                    PinsExist = true;
                                    PinRayMask |= rayMask;
                                }
                                // No friendly blocks, check.
                                else
                                {
                                    CheckRayMask |= rayMask;
                                    InDoubleCheck = InCheck;
                                    InCheck = true;
                                }

                                break;
                            }
                            else
                            {
                                // Opponent piece cannot check and blocks any other piece from checking.
                                break;
                            }
                        }
                    }
                }

                // Stop searching for pin if in double check, king must move.
                if (InDoubleCheck)
                {
                    break;
                }
            }

            // Knights.
            OpponentKnightAttacks = 0;
            var isKnightCheck = false;

            for (var knightIndex = 0;
                knightIndex < Board.Knights[Board.OpponentColourIndex].Count;
                knightIndex++)
            {

                var startIndex = Board.Knights[Board.OpponentColourIndex][knightIndex];
                OpponentKnightAttacks |= PrecomputedMoveData.KnightAttackBitboards[startIndex];

                if (!isKnightCheck && ContainsIndex(OpponentKnightAttacks, Board.Kings[Board.ColourIndex]))
                {
                    isKnightCheck = true;
                    InDoubleCheck = InCheck;
                    InCheck = true;
                    CheckRayMask |= 1ul << startIndex;
                }
            }

            // Pawns
            OpponentPawnAttackMap = 0;
            var isPawnCheck = false;

            for (int pawnIndex = 0; pawnIndex < Board.Pawns[Board.OpponentColourIndex].Count; pawnIndex++)
            {
                var index = Board.Pawns[Board.OpponentColourIndex][pawnIndex];
                ulong pawnAttacks =
                    PrecomputedMoveData.PawnAttackBitboards[index][Board.OpponentColourIndex];
                OpponentPawnAttackMap |= pawnAttacks;

                if (!isPawnCheck && ContainsIndex(pawnAttacks, Board.Kings[Board.ColourIndex]))
                {
                    isPawnCheck = true;
                    InDoubleCheck = InCheck;
                    InCheck = true;
                    CheckRayMask |= 1ul << index;
                }
            }

            var opponentKingIndex = Board.Kings[Board.OpponentColourIndex];

            OpponentAttackMapNoPawns = OpponentSlidingAttackMap | OpponentKnightAttacks |
                                       PrecomputedMoveData.KingAttackBitboards[opponentKingIndex];
            OpponentAttackMap = OpponentAttackMapNoPawns | OpponentPawnAttackMap;

            // DEBUG
            //Board.Print(OpponentAttackMap);
            //Console.ReadKey();
        }

        private void UpdateSlidingAttack(int startIndex, int startDir, int endDir)
        {
            for (var dir = startDir; dir < endDir; dir++)
            {
                var dirOffset = PrecomputedMoveData.DirectionOffsets[dir];
                for (var n = 0; n < PrecomputedMoveData.NumSquaresToEdge[startIndex][dir]; n++)
                {
                    var targetIndex = startIndex + dirOffset * (n + 1);
                    var targetPiece = Board.BoardArray[targetIndex];
                    OpponentSlidingAttackMap |= 1ul << targetIndex;
                    if (targetIndex != Board.Kings[Board.ColourIndex])
                    {
                        if (targetPiece != Piece.Empty)
                        {
                            break;
                        }
                    }
                }
            }
        }

        public static bool ContainsIndex(ulong bitboard, int index)
        {
            return ((bitboard >> index) & 1) != 0;
        }

        private bool IndexAttacked(int index)
        {
            return ContainsIndex(OpponentAttackMap, index);
        }

        private bool CheckAfterEnPassant(int startIndex, int targetIndex, int epIndex)
        {
            // Update board.
            Board.BoardArray[targetIndex] = Board.BoardArray[startIndex];
            Board.BoardArray[startIndex] = Piece.Empty;
            Board.BoardArray[epIndex] = Piece.Empty;

            bool inCheckAfterEnPassant = AttackedAfterEp(epIndex, startIndex);

            // Undo board change.
            Board.BoardArray[targetIndex] = Piece.Empty;
            Board.BoardArray[startIndex] = Piece.Pawn | Board.Colour;
            Board.BoardArray[epIndex] = Piece.Pawn | Board.OpponentColour;

            return inCheckAfterEnPassant;
        }

        private bool AttackedAfterEp(int epIndex, int startIndex)
        {
            if (ContainsIndex(OpponentAttackMapNoPawns, Board.Kings[Board.ColourIndex]))
            {
                return true;
            }

            var dirIndex = (epIndex < Board.Kings[Board.ColourIndex]) ? 2 : 3;
            for (var i = 0;
                i <
                PrecomputedMoveData.NumSquaresToEdge[Board.Kings[Board.ColourIndex]][dirIndex];
                i++)
            {
                var index = Board.Kings[Board.ColourIndex] +
                            PrecomputedMoveData.DirectionOffsets[dirIndex] * (i + 1);
                var piece = Board.BoardArray[index];

                if (piece != Piece.Empty)
                {
                    // Friendly piece blocks index.
                    if (Piece.IsColour(piece, Board.Colour))
                    {
                        break;
                    }
                    // Opponent piece is here.
                    else
                    {
                        if (Piece.IsRorQ(piece))
                        {
                            return true;
                        }
                        else
                        {
                            // This piece cannot move in this direction.
                            break;
                        }
                    }
                }
            }

            // Check if opponent pawn can see this index.
            for (var i = 0; i < 2; i++)
            {
                // Check if index exists diagonal to king where pawn could attack.
                if (PrecomputedMoveData.NumSquaresToEdge[Board.Kings[Board.ColourIndex]][
                    PrecomputedMoveData.PawnAttackDirections[Board.ColourIndex][i]] > 0)
                {
                    var piece = Board.BoardArray[Board.Kings[Board.ColourIndex] +
                                                 PrecomputedMoveData.DirectionOffsets[
                                                     PrecomputedMoveData.PawnAttackDirections[
                                                         Board.ColourIndex][i]]];
                    // Opponent pawn.
                    if (piece == (Piece.Pawn | Board.OpponentColour))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void GenKingMoves()
        {
            for (var i = 0; i < PrecomputedMoveData.KingMoves[Board.Kings[Board.ColourIndex]].Length; i++)
            {
                var targetIndex = PrecomputedMoveData.KingMoves[Board.Kings[Board.ColourIndex]][i];
                var pieceOnTarget = Board.BoardArray[targetIndex];

                // Can't move to index occupied by friendly piece.
                if (Piece.IsColour(pieceOnTarget, Board.Colour))
                {
                    continue;
                }

                var isCapture = Piece.IsColour(pieceOnTarget, Board.OpponentColour);
                if (!isCapture)
                {
                    // King must capture if going here, and skip if not generating quiet moves.
                    if (!DoQuietMoves || IndexInCheck(targetIndex))
                    {
                        continue;
                    }
                }

                // Safe index for king.
                if (!IndexAttacked(targetIndex))
                {
                    Moves.Add(new Move(Board.Kings[Board.ColourIndex], targetIndex, Piece.Empty));

                    // Castling
                    if (!InCheck && !isCapture)
                    {
                        // Kingside
                        if ((targetIndex == Board.IndexFromCoord(5, 0) ||
                            targetIndex == Board.IndexFromCoord(5, 7)) && KingSideCastleRight)
                        {
                            var kingSideIndex = targetIndex + 1;
                            if (Board.BoardArray[kingSideIndex] == Piece.Empty)
                            {
                                if (!IndexAttacked(kingSideIndex))
                                {
                                    Moves.Add(new Move(Board.Kings[Board.ColourIndex], kingSideIndex,
                                        Piece.Empty));
                                }
                            }
                        }
                        // Queenside
                        else if ((targetIndex == Board.IndexFromCoord(3, 0) ||
                                 targetIndex == Board.IndexFromCoord(3, 7)) && QueenSideCastleRight)
                        {
                            var queenSideIndex = targetIndex - 1;
                            if (Board.BoardArray[queenSideIndex] == Piece.Empty &&
                                Board.BoardArray[queenSideIndex - 1] == Piece.Empty)
                            {
                                if (!IndexAttacked(queenSideIndex))
                                {
                                    Moves.Add(new Move(Board.Kings[Board.ColourIndex], queenSideIndex,
                                        Piece.Empty));
                                }
                            }
                        }
                    }
                }
            }
        }

        private void GenSlidingMoves()
        {
            PieceList rooks = Board.Rooks[Board.ColourIndex];
            for (var i = 0; i < rooks.Count; i++)
            {
                GenSlidingPieceMoves(rooks[i], 0, 4);
            }

            PieceList bishops = Board.Bishops[Board.ColourIndex];
            for (var i = 0; i < bishops.Count; i++)
            {
                GenSlidingPieceMoves(bishops[i], 4, 8);
            }

            PieceList queens = Board.Queens[Board.ColourIndex];
            for (var i = 0; i < queens.Count; i++)
            {
                GenSlidingPieceMoves(queens[i], 0, 8);
            }
        }

        private void GenSlidingPieceMoves(int startIndex, int startDir, int endDir)
        {
            var isPinned = IsPinned(startIndex);

            // If piece is pinned and king in check, this piece can't move.
            if (InCheck && isPinned)
            {
                return;
            }

            for (var dirIndex = startDir; dirIndex < endDir; dirIndex++)
            {
                var dirOffset = PrecomputedMoveData.DirectionOffsets[dirIndex];

                // If pinned, this piece can only move where it maintains the pin.
                if (isPinned && !MovingAlongRay(dirOffset, Board.Kings[Board.ColourIndex], startIndex))
                {
                    continue;
                }

                for (var n = 0; n < PrecomputedMoveData.NumSquaresToEdge[startIndex][dirIndex]; n++)
                {
                    var targetIndex = startIndex + dirOffset * (n + 1);
                    var targetPiece = Board.BoardArray[targetIndex];

                    // Friendly piece in the way, stop for this direction.
                    if (Piece.IsColour(targetPiece, Board.Colour))
                    {
                        break;
                    }

                    var isCapture = targetPiece != Piece.Empty;
                    var preventsCheck = IndexInCheck(targetIndex);
                    if (preventsCheck || !InCheck)
                    {
                        if (DoQuietMoves || isCapture)
                        {
                            Moves.Add(new Move(startIndex, targetIndex, Piece.Empty));
                        }
                    }

                    // If index not empty, can't move any further, or if blocking a check, further moves won't block it.
                    if (isCapture || preventsCheck)
                    {
                        break;
                    }
                }
            }
        }

        private void GenKnightMoves()
        {
            PieceList knights = Board.Knights[Board.ColourIndex];

            for (var i = 0; i < knights.Count; i++)
            {
                var startIndex = knights[i];

                // Cannot move if pinned.
                if (IsPinned(startIndex))
                {
                    continue;
                }

                for (var index = 0; index < PrecomputedMoveData.KnightMoves[startIndex].Length; index++)
                {
                    var targetIndex = PrecomputedMoveData.KnightMoves[startIndex][index];
                    var targetPiece = Board.BoardArray[targetIndex];
                    var isCapture = Piece.IsColour(targetPiece, Board.OpponentColour);

                    if (DoQuietMoves || isCapture)
                    {
                        // Skip if target piece is friendly, or if in check and move won't block check.
                        if (Piece.IsColour(targetPiece, Board.Colour) ||
                            (InCheck && !IndexInCheck(targetIndex)))
                        {
                            continue;
                        }

                        Moves.Add(new Move(startIndex, targetIndex, Piece.Empty));
                    }
                }
            }
        }

        private void GenPawnMoves()
        {
            PieceList pawns = Board.Pawns[Board.ColourIndex];
            var pawnOffset = (Board.Colour == Piece.White) ? 8 : -8;
            var startRank = IsWhiteToMove ? 1 : 6;
            var rankBeforePromotion = IsWhiteToMove ? 6 : 1;
            var epFile = Board.Files.IndexOf(Board.Fen.EnPassant[0]);
            var epIndex = -1;
            if (epFile != -1)
            {
                epIndex = 8 * (Board.Fen.IsWhiteToMove ? 5 : 2) + epFile;
            }

            for (var i = 0; i < pawns.Count; i++)
            {
                var startIndex = pawns[i];
                var rank = Board.RankIndex(startIndex);
                var canPromote = rank == rankBeforePromotion;

                if (DoQuietMoves)
                {
                    var indexOneForward = startIndex + pawnOffset;

                    // Square ahead is empty.
                    if (indexOneForward < 64 && Board.BoardArray[indexOneForward] == Piece.Empty)
                    {
                        // Pawn not pinned, or moving within pin.
                        if (!IsPinned(startIndex) ||
                            MovingAlongRay(pawnOffset, startIndex, Board.Kings[Board.ColourIndex]))
                        {
                            // No check, or pawn blocking
                            if (!InCheck || IndexInCheck(indexOneForward))
                            {
                                if (canPromote)
                                {
                                    Moves.Add(new Move(startIndex, indexOneForward, Piece.Queen | Board.Colour));
                                    Moves.Add(new Move(startIndex, indexOneForward, Piece.Rook | Board.Colour));
                                    Moves.Add(new Move(startIndex, indexOneForward,
                                        Piece.Bishop | Board.Colour));
                                    Moves.Add(new Move(startIndex, indexOneForward,
                                        Piece.Knight | Board.Colour));
                                }
                                else
                                {
                                    Moves.Add(new Move(startIndex, indexOneForward, Piece.Empty));
                                }
                            }

                            // Can move forward two if on starting index.
                            if (rank == startRank)
                            {
                                var indexTwoForward = indexOneForward + pawnOffset;
                                if (Board.BoardArray[indexTwoForward] == Piece.Empty)
                                {
                                    // Not in check, or pawn blocking
                                    if (!InCheck || IndexInCheck(indexTwoForward))
                                    {
                                        Moves.Add(new Move(startIndex, indexTwoForward, Piece.Empty));
                                    }
                                }
                            }
                        }
                    }
                }

                // Captures
                for (var j = 0; j < 2; j++)
                {
                    // Check if diagonal exists.
                    if (PrecomputedMoveData.NumSquaresToEdge[startIndex][
                        PrecomputedMoveData.PawnAttackDirections[Board.ColourIndex][j]] > 0)
                    {
                        // Can pawn capture
                        var captureDir =
                            PrecomputedMoveData.DirectionOffsets[
                                PrecomputedMoveData.PawnAttackDirections[Board.ColourIndex][j]];
                        var targetIndex = startIndex + captureDir;
                        var targetPiece = Board.BoardArray[targetIndex];

                        // If pinned, must remain on same line as pin
                        if (IsPinned(startIndex) && !MovingAlongRay(captureDir,
                            Board.Kings[Board.ColourIndex], startIndex))
                        {
                            continue;
                        }

                        // Simple capture.
                        if (Piece.IsColour(targetPiece, Board.OpponentColour))
                        {
                            // If in check and not capturing the checking piece, skip.
                            if (InCheck && !IndexInCheck(targetIndex))
                            {
                                continue;
                            }

                            if (canPromote)
                            {
                                Moves.Add(new Move(startIndex, targetIndex, Piece.Queen | Board.Colour));
                                Moves.Add(new Move(startIndex, targetIndex, Piece.Rook | Board.Colour));
                                Moves.Add(new Move(startIndex, targetIndex, Piece.Bishop | Board.Colour));
                                Moves.Add(new Move(startIndex, targetIndex, Piece.Knight | Board.Colour));
                            }
                            else
                            {
                                Moves.Add(new Move(startIndex, targetIndex, Piece.Empty));
                            }
                        }

                        // En Passant
                        if (targetIndex == epIndex)
                        {
                            var epCaptureIndex = targetIndex + (IsWhiteToMove ? -8 : 8);
                            if (!CheckAfterEnPassant(startIndex, targetIndex, epCaptureIndex))
                            {
                                Moves.Add(new Move(startIndex, targetIndex, Piece.Empty));
                            }
                        }
                    }
                }
            }
        }

        private bool IndexInCheck(int index)
        {
            return InCheck && ((CheckRayMask >> index) & 1) != 0;
        }

        private bool IsPinned(int index)
        {
            return PinsExist && ((PinRayMask >> index) & 1) != 0;
        }

        private bool MovingAlongRay(int rayDir, int startIndex, int targetIndex)
        {
            var moveDir = PrecomputedMoveData.DirectionLookup[targetIndex - startIndex + 63];
            return (rayDir == moveDir || -rayDir == moveDir);
        }

        private bool KingSideCastleRight
        {
            get
            {
                var mask = (Board.ColourIndex == Board.WhiteIndex) ? 'K' : 'k';
                return (Board.Fen.CastlingRights.Contains(mask));
            }
        }

        private bool QueenSideCastleRight
        {
            get
            {
                var mask = (Board.ColourIndex == Board.WhiteIndex) ? 'Q' : 'q';
                return (Board.Fen.CastlingRights.Contains(mask));
            }
        }
    }

    internal static class PrecomputedMoveData
    {
        /**
         * INFO:
         * This internal class is an altered version from Sebastian Lague's Chess-AI Project
         * which can be found here: https://github.com/SebLague/Chess-AI/
         * Adjustments were made to fit into this program, license permitting.
         */

        // First 4 are orthogonal, last 4 are diagonals (N, S, W, E, NW, SE, NE, SW)
        public static readonly int[] DirectionOffsets = { 8, -8, -1, 1, 7, -7, 9, -9 };

        // Stores number of moves available in each of the 8 directions for every square on the board
        // Order of directions is: N, S, W, E, NW, SE, NE, SW
        // So for example, if availableSquares[0][1] == 7...
        // that means that there are 7 squares to the north of b1 (the square with index 1 in board array)
        public static readonly int[][] NumSquaresToEdge;

        // Stores array of indices for each square a knight can land on from any square on the board
        // So for example, KnightMoves[0] is equal to {10, 17}, meaning a knight on a1 can jump to c2 and b3
        public static readonly byte[][] KnightMoves;
        public static readonly byte[][] KingMoves;

        // Pawn attack directions for white and black (NW, NE; SW SE)
        public static readonly byte[][] PawnAttackDirections =
        {
            new byte[] {4, 6},
            new byte[] {7, 5}
        };

        public static readonly int[][] PawnAttacksWhite;
        public static readonly int[][] PawnAttacksBlack;
        public static readonly int[] DirectionLookup;

        public static readonly ulong[] KingAttackBitboards;
        public static readonly ulong[] KnightAttackBitboards;
        public static readonly ulong[][] PawnAttackBitboards;

        public static readonly ulong[] RookMoves;
        public static readonly ulong[] BishopMoves;
        public static readonly ulong[] QueenMoves;

        // Aka manhattan distance (answers how many moves for a rook to get from square a to square b)
        public static int[,] OrthogonalDistance;

        // Aka chebyshev distance (answers how many moves for a king to get from square a to square b)
        public static int[,] KingDistance;
        public static int[] CentreManhattanDistance;

        public static int NumRookMovesToReachSquare(int startSquare, int targetSquare)
        {
            return OrthogonalDistance[startSquare, targetSquare];
        }

        public static int NumKingMovesToReachSquare(int startSquare, int targetSquare)
        {
            return KingDistance[startSquare, targetSquare];
        }

        // Initialize lookup data
        static PrecomputedMoveData()
        {
            PawnAttacksWhite = new int[64][];
            PawnAttacksBlack = new int[64][];
            NumSquaresToEdge = new int[8][];
            KnightMoves = new byte[64][];
            KingMoves = new byte[64][];
            NumSquaresToEdge = new int[64][];

            RookMoves = new ulong[64];
            BishopMoves = new ulong[64];
            QueenMoves = new ulong[64];

            // Calculate knight jumps and available squares for each square on the board.
            // See comments by variable definitions for more info.
            int[] allKnightJumps = { 15, 17, -17, -15, 10, -6, 6, -10 };
            KnightAttackBitboards = new ulong[64];
            KingAttackBitboards = new ulong[64];
            PawnAttackBitboards = new ulong[64][];

            for (int squareIndex = 0; squareIndex < 64; squareIndex++)
            {

                int y = squareIndex / 8;
                int x = squareIndex - y * 8;

                int north = 7 - y;
                int south = y;
                int west = x;
                int east = 7 - x;
                NumSquaresToEdge[squareIndex] = new int[8];
                NumSquaresToEdge[squareIndex][0] = north;
                NumSquaresToEdge[squareIndex][1] = south;
                NumSquaresToEdge[squareIndex][2] = west;
                NumSquaresToEdge[squareIndex][3] = east;
                NumSquaresToEdge[squareIndex][4] = Math.Min(north, west);
                NumSquaresToEdge[squareIndex][5] = Math.Min(south, east);
                NumSquaresToEdge[squareIndex][6] = Math.Min(north, east);
                NumSquaresToEdge[squareIndex][7] = Math.Min(south, west);

                // Calculate all squares knight can jump to from current square
                var legalKnightJumps = new List<byte>();
                ulong knightBitboard = 0;
                foreach (int knightJumpDelta in allKnightJumps)
                {
                    int knightJumpSquare = squareIndex + knightJumpDelta;
                    if (knightJumpSquare >= 0 && knightJumpSquare < 64)
                    {
                        int knightSquareY = knightJumpSquare / 8;
                        int knightSquareX = knightJumpSquare - knightSquareY * 8;
                        // Ensure knight has moved max of 2 squares on x/y axis (to reject indices that have wrapped around side of board)
                        int maxCoordMoveDst = Math.Max(Math.Abs(x - knightSquareX), Math.Abs(y - knightSquareY));
                        if (maxCoordMoveDst == 2)
                        {
                            legalKnightJumps.Add((byte)knightJumpSquare);
                            knightBitboard |= 1ul << knightJumpSquare;
                        }
                    }
                }

                KnightMoves[squareIndex] = legalKnightJumps.ToArray();
                KnightAttackBitboards[squareIndex] = knightBitboard;

                // Calculate all squares king can move to from current square (not including castling)
                var legalKingMoves = new List<byte>();
                foreach (int kingMoveDelta in DirectionOffsets)
                {
                    int kingMoveSquare = squareIndex + kingMoveDelta;
                    if (kingMoveSquare >= 0 && kingMoveSquare < 64)
                    {
                        int kingSquareY = kingMoveSquare / 8;
                        int kingSquareX = kingMoveSquare - kingSquareY * 8;
                        // Ensure king has moved max of 1 square on x/y axis (to reject indices that have wrapped around side of board)
                        int maxCoordMoveDst = Math.Max(Math.Abs(x - kingSquareX), Math.Abs(y - kingSquareY));
                        if (maxCoordMoveDst == 1)
                        {
                            legalKingMoves.Add((byte)kingMoveSquare);
                            KingAttackBitboards[squareIndex] |= 1ul << kingMoveSquare;
                        }
                    }
                }

                KingMoves[squareIndex] = legalKingMoves.ToArray();

                // Calculate legal pawn captures for white and black
                List<int> pawnCapturesWhite = new List<int>();
                List<int> pawnCapturesBlack = new List<int>();
                PawnAttackBitboards[squareIndex] = new ulong[2];
                if (x > 0)
                {
                    if (y < 7)
                    {
                        pawnCapturesWhite.Add(squareIndex + 7);
                        PawnAttackBitboards[squareIndex][Board.WhiteIndex] |= 1ul << (squareIndex + 7);
                    }

                    if (y > 0)
                    {
                        pawnCapturesBlack.Add(squareIndex - 9);
                        PawnAttackBitboards[squareIndex][Board.BlackIndex] |= 1ul << (squareIndex - 9);
                    }
                }

                if (x < 7)
                {
                    if (y < 7)
                    {
                        pawnCapturesWhite.Add(squareIndex + 9);
                        PawnAttackBitboards[squareIndex][Board.WhiteIndex] |= 1ul << (squareIndex + 9);
                    }

                    if (y > 0)
                    {
                        pawnCapturesBlack.Add(squareIndex - 7);
                        PawnAttackBitboards[squareIndex][Board.BlackIndex] |= 1ul << (squareIndex - 7);
                    }
                }

                PawnAttacksWhite[squareIndex] = pawnCapturesWhite.ToArray();
                PawnAttacksBlack[squareIndex] = pawnCapturesBlack.ToArray();

                // Rook moves
                for (int directionIndex = 0; directionIndex < 4; directionIndex++)
                {
                    int currentDirOffset = DirectionOffsets[directionIndex];
                    for (int n = 0; n < NumSquaresToEdge[squareIndex][directionIndex]; n++)
                    {
                        int targetSquare = squareIndex + currentDirOffset * (n + 1);
                        RookMoves[squareIndex] |= 1ul << targetSquare;
                    }
                }

                // Bishop moves
                for (int directionIndex = 4; directionIndex < 8; directionIndex++)
                {
                    int currentDirOffset = DirectionOffsets[directionIndex];
                    for (int n = 0; n < NumSquaresToEdge[squareIndex][directionIndex]; n++)
                    {
                        int targetSquare = squareIndex + currentDirOffset * (n + 1);
                        BishopMoves[squareIndex] |= 1ul << targetSquare;
                    }
                }

                QueenMoves[squareIndex] = RookMoves[squareIndex] | BishopMoves[squareIndex];
            }

            DirectionLookup = new int[127];
            for (int i = 0; i < 127; i++)
            {
                int offset = i - 63;
                int absOffset = System.Math.Abs(offset);
                int absDir = 1;
                if (absOffset % 9 == 0)
                {
                    absDir = 9;
                }
                else if (absOffset % 8 == 0)
                {
                    absDir = 8;
                }
                else if (absOffset % 7 == 0)
                {
                    absDir = 7;
                }

                DirectionLookup[i] = absDir * System.Math.Sign(offset);
            }

            // Distance lookup
            OrthogonalDistance = new int[64, 64];
            KingDistance = new int[64, 64];
            CentreManhattanDistance = new int[64];
            for (int squareA = 0; squareA < 64; squareA++)
            {
                var coordA = Board.CoordFromIndex(squareA);
                int fileDstFromCentre = Math.Max(3 - coordA.Item1, coordA.Item1 - 4);
                int rankDstFromCentre = Math.Max(3 - coordA.Item2, coordA.Item2 - 4);
                CentreManhattanDistance[squareA] = fileDstFromCentre + rankDstFromCentre;

                for (int squareB = 0; squareB < 64; squareB++)
                {

                    var coordB = Board.CoordFromIndex(squareB);
                    int rankDistance = Math.Abs(coordA.Item2 - coordB.Item2);
                    int fileDistance = Math.Abs(coordA.Item1 - coordB.Item1);
                    OrthogonalDistance[squareA, squareB] = fileDistance + rankDistance;
                    KingDistance[squareA, squareB] = Math.Max(fileDistance, rankDistance);
                }
            }
        }
    }

    internal class PieceList
    {
        /**
         * INFO:
         * This internal class is an altered version from Sebastian Lague's Chess-AI Project
         * which can be found here: https://github.com/SebLague/Chess-AI/
         * Adjustments were made to fit into this program, license permitting.
         */
        public int[] locations;
        public int[] Map;
        public int NumPieces;

        public PieceList(int maxCount = 16)
        {
            locations = new int[maxCount];
            Map = new int[64];
            NumPieces = 0;
        }

        public void AddPiece(int index)
        {
            try
            {
                locations[NumPieces] = index;
                Map[index] = NumPieces;
                NumPieces++;
            }
            catch (Exception e)
            {
                if (ChessGame.EnableDebugging)
                {
                    ChessGame.StateFile.WriteLine("Failed to add piece to piece list");
                    ChessGame.StateFile.WriteLine(e.Message);
                }
            }
        }

        public void DelPiece(int index)
        {
            try
            {
                var pIndex = Map[index];
                locations[pIndex] = locations[NumPieces - 1];
                Map[locations[pIndex]] = pIndex;
                NumPieces--;
            }
            catch (Exception e)
            {
                if (ChessGame.EnableDebugging)
                {
                    ChessGame.StateFile.WriteLine("Failed to remove piece from piece list");
                    ChessGame.StateFile.WriteLine(e.Message);
                }
            }
        }

        public void MovePiece(int startIndex, int targetIndex)
        {
            var pIndex = Map[startIndex];
            locations[pIndex] = targetIndex;
            Map[targetIndex] = pIndex;
        }

        public int Count
        {
            get { return NumPieces; }
        }

        // Quicker access to locations.
        public int this[int index] => locations[index];
    }

    internal static class Piece
    {
        /**
         * INFO:
         * This internal class is an altered version from Sebastian Lague's Chess-AI Project
         * which can be found here: https://github.com/SebLague/Chess-AI/
         * Adjustments were made to fit into this program, license permitting.
         */
        public const int Empty = 0;
        public const int King = 1;
        public const int Pawn = 2;
        public const int Knight = 3;
        public const int Bishop = 5;
        public const int Rook = 6;
        public const int Queen = 7;

        public const int White = 8;
        public const int Black = 16;

        public const int typeMask = 0b00111;
        public const int blackMask = 0b10000;
        public const int whiteMask = 0b01000;
        public const int colourMask = whiteMask | blackMask;

        public static Dictionary<int, char> SymbolFromPiece = new Dictionary<int, char>()
        {
            [Empty] = ' ',
            [King] = 'k',
            [Pawn] = 'p',
            [Knight] = 'n',
            [Bishop] = 'b',
            [Rook] = 'r',
            [Queen] = 'q',
        };

        public static Dictionary<char, int> PieceFromSymbol = new Dictionary<char, int>()
        {
            ['-'] = Empty,
            ['k'] = King,
            ['p'] = Pawn,
            ['n'] = Knight,
            ['b'] = Bishop,
            ['r'] = Rook,
            ['q'] = Queen,
        };

        public static int GetType(int piece)
        {
            return piece & typeMask;
        }

        public static int GetColour(int piece)
        {
            return piece & colourMask;
        }

        public static bool IsColour(int piece, int colour)
        {
            return (piece & colourMask) == colour;
        }

        public static bool IsRorQ(int piece)
        {
            return (piece & 0b110) == 0b110;
        }

        public static bool IsBorQ(int piece)
        {
            return (piece & 0b101) == 0b101;
        }

        public static bool IsSliding(int piece)
        {
            return (piece & 0b100) != 0;
        }

        public static bool IsValidMove(int piece, Move move)
        {
            //switch (piece & typeMask)
            //{
            //    case Pawn:
            //        break;
            //    case 
            //}

            return true;
        }
    }

    internal readonly struct Move
    {
        public readonly string Name;
        public readonly int Eval;
        public readonly bool Undo;
        public readonly bool Quit;

        public readonly int From;
        public readonly int To;
        public readonly int Promotion;

        public Move(Move move, int eval = 0)
        {
            Name = move.Name;
            Eval = eval;
            Undo = move.Undo;
            Quit = move.Quit;
            From = move.From;
            To = move.To;
            Promotion = move.Promotion;
        }

        public Move(string name, int colour, int eval = 0)
        {
            Name = name;
            Eval = eval;
            Undo = false;
            Quit = false;
            From = 0;
            To = 0;
            Promotion = 0;

            try
            {
                // Parse move.
                var moveArray = name.Split(' ');
                var moveFromFile = Board.Files.IndexOf(moveArray[0][0]);
                var moveFromRank = (int)char.GetNumericValue(moveArray[0][1]) - 1;
                var moveToFile = Board.Files.IndexOf(moveArray[1][0]);
                var moveToRank = (int)char.GetNumericValue(moveArray[1][1]) - 1;

                From = Board.IndexFromCoord(moveFromFile, moveFromRank);
                To = Board.IndexFromCoord(moveToFile, moveToRank);

                if (moveArray.Length >= 3)
                {
                    Promotion = Piece.PieceFromSymbol[moveArray[2][0]] | colour;
                }
            }
            catch (Exception e)
            {
                ChessGame.StateFile.WriteLine($"Move parsing failed: {name}");
                ChessGame.StateFile.WriteLine(e.Message);
            }
        }

        public Move(int moveFrom, int moveTo, int promotion, int eval = 0)
        {
            Name = Board.NameFromCoordinate(Board.FileIndex(moveFrom), Board.RankIndex(moveFrom));
            Name += " ";
            Name += Board.NameFromCoordinate(Board.FileIndex(moveTo), Board.RankIndex(moveTo));
            From = moveFrom;
            To = moveTo;
            Promotion = promotion;
            Undo = false;
            Quit = false;

            if (Promotion != Piece.Empty)
            {
                Name += Piece.SymbolFromPiece[promotion & Piece.typeMask];
            }

            Eval = eval;
        }

        private Move(bool undo, bool quit = false)
        {
            Name = string.Empty;
            Eval = 0;
            Undo = undo;
            From = -1;
            To = -1;
            Promotion = -1;
            Quit = quit;
        }

        public static Move Invalid
        {
            get
            {
                return new Move();
            }
        }

        public static Move QuitMove
        {
            get
            {
                return new Move(false, true);
            }
        }

        public static Move UndoMove
        {
            get
            {
                return new Move(true);
            }
        }

        public bool isInvalid
        {
            get
            {
                return From == 0 && To == 0;
            }
        }

        public static bool SameMove(Move move1, Move move2)
        {
            return move1.From == move2.From && move1.To == move2.To && move1.Promotion == move2.Promotion;
        }
    }

    internal class Player
    {
        public int ColourIndex;
        public int Colour;
        public Player Opponent;

        public Player(int colourIndex, Player opponent = null)
        {
            ColourIndex = colourIndex;
            Colour = (colourIndex + 1) * 8;
            Opponent = opponent;
            if (opponent != null)
            {
                opponent.Opponent = this;
            }
        }

        public virtual Move GetMove(Board board, List<Move> possibleMoves)
        {
            while (true)
            {
                if (ChessGame.EnableDebugging || ChessGame.EnableHelp)
                {
                    Console.WriteLine("Possible Moves:");
                    foreach (var pMove in possibleMoves)
                    {
                        Console.Write(
                            $"{Piece.SymbolFromPiece[board.BoardArray[pMove.From] & Piece.typeMask]}: ({pMove.Name}), ");
                    }
                }
                Console.WriteLine(
                    "\n\nEnter move in format of from-to (eg: [a2 a3] or [a7 a8 q]), [a-h][1-8] [a-h][1-8] ?[nbrq]:");
                Console.Write(": ");
                var input = Console.ReadLine();
                if (string.IsNullOrEmpty(input))
                {
                    board.Print();
                    Console.WriteLine("Move not found, please try again.");
                    continue;
                }

                if (ChessGame.EnableDebugging && input.Contains("debug"))
                {
                    try
                    {
                        var pieceType = Piece.PieceFromSymbol[input.Split(' ')[1][0]];
                        if (pieceType == Piece.King)
                        {
                            var kFriendly = board.Kings[ColourIndex];
                            var kOpponent = board.Kings[Opponent.ColourIndex];
                            Console.WriteLine($"Index for Friendly: {kFriendly}, Opponent: {kOpponent}");
                        }
                        else
                        {
                            var locFriendly = board.GetPieces(pieceType, ColourIndex);
                            Console.WriteLine("Friendly locations");
                            for (var i = 0; i < locFriendly.Count; i++)
                            {
                                Console.Write($"{locFriendly[i]}, ");
                            }
                            Console.WriteLine();
                            var mapFriendly = board.GetPieces(pieceType, ColourIndex).Map;
                            for (var rank = 7; rank >= 0; rank--)
                            {
                                for (var file = 0; file < 8; file++)
                                {
                                    Console.Write($" {mapFriendly[rank * 8 + file]} ");
                                }

                                Console.WriteLine();
                            }
                            Console.WriteLine();

                            var locOpponent = board.GetPieces(pieceType, Opponent.ColourIndex);
                            Console.WriteLine("Opponent locations");
                            for (var i = 0; i < locOpponent.Count; i++)
                            {
                                Console.Write($"{locOpponent[i]}, ");
                            }
                            Console.WriteLine();
                            var pOpponent = board.GetPieces(pieceType, Opponent.ColourIndex).Map;
                            for (var rank = 7; rank >= 0; rank--)
                            {
                                for (var file = 0; file < 8; file++)
                                {
                                    Console.Write($" {pOpponent[rank * 8 + file]} ");
                                }

                                Console.WriteLine();
                            }

                            Console.WriteLine();
                        }
                    }
                    catch (Exception e)
                    {
                        ChessGame.StateFile.WriteLine("Debug parse failed");
                        ChessGame.StateFile.WriteLine(e.Message);
                    }
                    continue;
                }

                if (input.Contains("quit"))
                {
                    return Move.QuitMove;
                }

                if (input.Contains("u"))
                {
                    if (input.Length > 1)
                    {
                        return new Move(Move.UndoMove, int.Parse(input.Substring(1)));
                    }

                    return Move.UndoMove;
                }

                var move = ParseMove(board, possibleMoves, input);

                Console.WriteLine($"Move: {move.Name}");

                if (move.isInvalid || !possibleMoves.Exists(x =>
                    (x.To == move.To) && (x.From == move.From) && (x.Promotion == move.Promotion)))
                {
                    Console.WriteLine("Move not found, please try again.");
                    continue;
                }

                return move;
            }
        }

        public static Move ParseMove(Board board, List<Move> possibleMoves, string input)
        {
            var moveArray = input.Split(' ');

            try
            {
                if (moveArray.Length < 2)
                {
                    if (moveArray[0].Length == 2)
                    {
                        var pawnMovesWithDestination = possibleMoves.FindAll(x => (x.To == Board.IndexFromCoord(
                                                          Board.Files.IndexOf(moveArray[0][0]),
                                                          (int)char.GetNumericValue(moveArray[0][1]) - 1)) &&
                                                      (board.BoardArray[x.From] & Piece.typeMask) == Piece.Pawn);
                        if (pawnMovesWithDestination.Count == 1)
                        {
                            return pawnMovesWithDestination[0];
                        }
                        else
                        {
                            board.Print();
                            Console.WriteLine("Move not found, please try again.");
                            return Move.Invalid;
                        }
                    }
                    else if (moveArray[0].Length == 4)
                    {
                        moveArray = new[]
                            {$"{moveArray[0][0]}{moveArray[0][1]}", $"{moveArray[0][2]}{moveArray[0][3]}"};
                    }
                    else if (moveArray[0].Length == 5)
                    {
                        moveArray = new[]
                        {
                                $"{moveArray[0][0]}{moveArray[0][1]}", $"{moveArray[0][2]}{moveArray[0][3]}",
                                $"{moveArray[0][4]}"
                            };
                    }

                    input = string.Join(' ', moveArray);
                }

                if (!Board.Files.Contains(moveArray[0][0]) ||
                    !Board.Ranks.Contains(moveArray[0][1]) ||
                    !Board.Files.Contains(moveArray[1][0]) ||
                    !Board.Ranks.Contains(moveArray[1][1]))
                {
                    Console.WriteLine($"Move not found, please try again: {string.Join(string.Empty, moveArray)}");
                    return Move.Invalid;
                }
            }
            catch (Exception e)
            {
                ChessGame.StateFile.WriteLine("Player failed to get move.");
                ChessGame.StateFile.WriteLine(e.Message);
            }

            return new Move(input, board.Colour);
        }
    }

    internal class Ai : Player
    {
        private Search _Search;
        private int Depth;
        private bool IterativeDeepening;
        private int SearchTimeMillis;

        private Move Move;
        private int Eval;
        private bool MoveFound;
        private CancellationTokenSource CancelTimer;

        public Ai(int colourIndex, Player opponent = null) : base(colourIndex, opponent)
        {
            ColourIndex = colourIndex;
            Colour = (colourIndex + 1) * 8;
            Opponent = opponent;
            if (opponent != null)
            {
                opponent.Opponent = this;
            }

            Console.WriteLine($"{(ColourIndex == Board.WhiteIndex ? "White" : "Black")}");
            Console.WriteLine($"AI depth:");

            var input = Console.ReadLine();
            int result;
            if (!int.TryParse(input, out result))
            {
                result = 2;
            }
            Depth = result;

            Console.WriteLine($"Use iterative deepening: (y = yes, otherwise no)");
            var input2 = Console.ReadLine();
            IterativeDeepening = !string.IsNullOrEmpty(input2) && input2.Contains("y");

            if (IterativeDeepening)
            {
                Console.WriteLine($"Search Time limit (ms)");
                var input3 = Console.ReadLine();
                if (!int.TryParse(input3, out result))
                {
                    result = 5000;
                }
                SearchTimeMillis = result;
            }
        }

        public override Move GetMove(Board board, List<Move> possibleMoves)
        {
            MoveFound = false;
            Move = Move.Invalid;
            _Search = new Search(board);
            var book = new Book();
            SearchTimeMillis = 5000;
            var maxBookPly = 4;

            var bookMove = Move.Invalid;
            if (board.Fen.FullMoves <= maxBookPly)
            {
                // Find book move.
                bookMove = book.FindMove(board, possibleMoves);
            }

            while (!MoveFound)
            {
                if (bookMove.isInvalid)
                {
                    // Try multithreading.
                    //Task.Factory.StartNew(() => _Search.Start(Depth), TaskCreationOptions.LongRunning);
                    //CancelTimer = new CancellationTokenSource();
                    //Task.Delay(searchTimeMillis, CancelTimer.Token).ContinueWith((t) => TimeoutSearch());

                    // Let's not do multitasking...
                    _Search.Start(Depth, SearchTimeMillis, IterativeDeepening);
                    Move = _Search.BestMove;
                }
                else
                {
                    Move = bookMove;
                    MoveFound = true;
                }

                //// DEBUG
                //if (ChessGame.EnableDebugging)
                //{
                //    foreach (var move in possibleMoves)
                //    {
                //        Console.WriteLine($"possible move: {move.Name}, eval: {move.Eval}");
                //    }

                //    var mg = new MoveGenerator();
                //    var testMoves = mg.GenerateMoves(board);
                //    foreach (var move in testMoves)
                //    {
                //        Console.WriteLine($"test move: {move.Name}, eval: {move.Eval}");
                //    }

                //    Console.ReadKey();
                //}

                if (Move.isInvalid || !possibleMoves.Exists(x => x.From == Move.From &&
                                                                 x.To == Move.To && x.Promotion == Move.Promotion))
                {
                    Console.WriteLine(
                        $"Move was invalid: {Move.Name}, {Move.From} -> {Move.To}, {Move.Promotion}, {Move.Eval}");
                    Move = Move.Invalid;
                    while (Move.isInvalid)
                    {
                        Console.WriteLine("Ai Failed to get move, please enter move:");
                        var input = Console.ReadLine();
                        Move = ParseMove(board, possibleMoves, input);
                    }
                }
                else
                {
                    MoveFound = true;
                }
            }

            Thread.Sleep(1000);
            return Move;
        }

        private void TimeoutSearch()
        {
            if (CancelTimer == null || !CancelTimer.IsCancellationRequested)
            {
                _Search.EndSearch();
                (Move, Eval) = _Search.GetResult();
                MoveFound = true;
            }
        }

        private void OnSeachComplete(Move move)
        {
            CancelTimer?.Cancel();
            MoveFound = true;
            Move = move;
        }

        internal class Search
        {
            /**
         * INFO:
         * This internal class is an altered version from Sebastian Lague's Chess-AI Project
         * which can be found here: https://github.com/SebLague/Chess-AI/
         * Adjustments were made to fit into this program, license permitting.
         */
            public event Action<Move> OnSearchComplete;

            private const int TTableSize = 64000;
            public const int MateScore = 100000;
            private const int PosInf = 9999999;
            private const int NegInf = -PosInf;

            private TranspositionTable TTable;
            private MoveGenerator MoveGen;

            public Move BestMove;
            private int BestEval;
            private Move BestMoveForIteration;
            private int BestEvalForIteration;

            private int CurrentDepth;
            private bool Abort;

            private Move InvalidMove;
            private MoveOrderer MoveOrderer;
            private Board Board;
            private BoardEval BoardEval;

            private Stopwatch Stopwatch;

            public Search(Board board)
            {
                Board = board;
                BoardEval = new BoardEval();
                MoveGen = new MoveGenerator();
                TTable = new TranspositionTable(board, TTableSize);
                MoveOrderer = new MoveOrderer(MoveGen, TTable);
                InvalidMove = Move.Invalid;
            }

            public void Start(int targetDepth = 1, int timeLimitMillis = 5000, bool iterativeDeepening = false)
            {
                BestEval = BestEvalForIteration = 0;
                BestMove = BestMoveForIteration = Move.Invalid;
                TTable.Enabled = true;

                // TENTATIVE
                TTable.Clear();

                CurrentDepth = 0;
                Abort = false;

                if (iterativeDeepening)
                {
                    Stopwatch = new Stopwatch();
                    Stopwatch.Start();
                    // Use iterative deepening.
                    for (var depth = 1; depth <= targetDepth; depth++)
                    {
                        if (Stopwatch.ElapsedMilliseconds > timeLimitMillis)
                        {
                            Stopwatch.Stop();
                            CurrentDepth = depth;
                            BestMove = BestMoveForIteration;
                            BestEval = BestEvalForIteration;
                        }

                        SearchMoves(depth, ply: 0, alpha: NegInf, beta: PosInf);

                        //Checkmate found.
                        if (Math.Abs(BestEval) > MateScore - 1000)
                        {
                            //break;
                        }
                    }
                }
                else
                {
                    // No iterative deepening.
                    SearchMoves(targetDepth, 0, NegInf, PosInf);
                    BestMove = BestMoveForIteration;
                    BestEval = BestEvalForIteration;
                }

                OnSearchComplete?.Invoke(BestMove);
            }

            private int SearchMoves(int depth, int ply, int alpha, int beta)
            {
                if (ChessGame.EnableDebugging)
                    ChessGame.StateFile.WriteLine($"Searching depth: {depth}, ply: {ply}, a: {alpha}, b: {beta}");

                if (Abort)
                {
                    return 0;
                }

                if (ply > 0)
                {
                    alpha = Math.Max(alpha, -MateScore + ply);
                    beta = Math.Min(beta, MateScore - ply);
                    if (alpha >= beta)
                    {
                        return alpha;
                    }
                }

                var tTableEval = TTable.LookupEval(depth, ply, alpha, beta);
                if (tTableEval != TranspositionTable.Failed)
                {
                    if (ply == 0)
                    {
                        BestMoveForIteration = new Move(TTable.GetStoredMove(), TTable.Entries[TTable.Index].Value);
                        BestEvalForIteration = TTable.Entries[TTable.Index].Value;

                        if (ChessGame.EnableDebugging)
                            ChessGame.StateFile.WriteLine($"Found TT move: {BestMoveForIteration.Name}, depth: {TTable.Entries[TTable.Index].Depth}");
                    }

                    return tTableEval;
                }

                if (depth == 0)
                {
                    return NoCapSearch(alpha, beta);
                }

                var moves = MoveGen.GenerateMoves(Board);
                MoveOrderer.OrderMoves(Board, moves, true);

                if (moves.Count == 0)
                {
                    if (MoveGen.InCheck)
                    {
                        return ply - MateScore;
                    }

                    return 0;
                }

                var evalType = TranspositionTable.UpperBound;
                var bestMoveInPos = InvalidMove;

                for (var i = 0; i < moves.Count; i++)
                {
                    if (ChessGame.EnableDebugging)
                        ChessGame.StateFile.WriteLine($"Making move: {moves[i].Name}, eval: {moves[i].Eval}");
                    Board.MakeMove(moves[i]);
                    var eval = -SearchMoves(depth - 1, ply + 1, -beta, -alpha);
                    moves[i] = new Move(moves[i], eval);
                    if (ChessGame.EnableDebugging)
                        ChessGame.StateFile.WriteLine($"Undoing move, eval: {eval}");
                    Board.UndoMove();

                    // Prune
                    if (eval >= beta)
                    {
                        TTable.StoreEval(depth, ply, beta, TranspositionTable.LowerBound, moves[i]);
                        return beta;
                    }

                    // Better move.
                    if (eval > alpha)
                    {
                        evalType = TranspositionTable.Exact;
                        bestMoveInPos = moves[i];

                        alpha = eval;
                        if (ply == 0)
                        {
                            BestMoveForIteration = new Move(moves[i], eval);
                            BestEvalForIteration = eval;
                        }
                    }
                }

                TTable.StoreEval(depth, ply, alpha, evalType, bestMoveInPos);

                if (ChessGame.EnableDebugging)
                {
                    ChessGame.StateFile.WriteLine($"BMIP = depth: {depth}, ply: {ply}, move: {bestMoveInPos.Name}, eval: {bestMoveInPos.Eval}");
                    foreach (var move in moves)
                    {
                        ChessGame.StateFile.WriteLine($"move: {move.Name}:{move.Eval}");
                    }
                }
                    

                return alpha;
            }

            private int NoCapSearch(int alpha, int beta)
            {
                // There may be a better non-cap move than any cap move.
                var eval = BoardEval.Eval(Board);

                if (eval >= beta)
                {
                    return beta;
                }

                if (eval > alpha)
                {
                    alpha = eval;
                }

                var moves = MoveGen.GenerateMoves(Board, false);
                MoveOrderer.OrderMoves(Board, moves, false);
                for (var i = 0; i < moves.Count; i++)
                {
                    if (ChessGame.EnableDebugging)
                        ChessGame.StateFile.WriteLine($"Making move: {moves[i].Name}, eval: {moves[i].Eval}");
                    Board.MakeMove(moves[i]);
                    eval = -NoCapSearch(-alpha, -beta);
                    moves[i] = new Move(moves[i], eval);
                    if (ChessGame.EnableDebugging)
                        ChessGame.StateFile.WriteLine($"Undoing move, eval: {eval}");
                    Board.UndoMove();

                    if (eval >= beta)
                    {
                        return beta;
                    }

                    if (eval > alpha)
                    {
                        alpha = eval;
                    }
                }

                return alpha;
            }

            public void EndSearch()
            {
                Abort = true;
            }

            public (Move move, int eval) GetResult()
            {
                return (BestMove, BestEval);
            }
        }

        internal class MoveOrderer
        {
            /**
         * INFO:
         * This internal class is an altered version from Sebastian Lague's Chess-AI Project
         * which can be found here: https://github.com/SebLague/Chess-AI/
         * Adjustments were made to fit into this program, license permitting.
         */
            private int[] moveEvals;

            private const int maxMoveCount = 218;
            private const int IndexSeenByOpponentPawnPenalty = 350;
            private const int CaptureValueMultiplier = 10;

            private MoveGenerator moveGen;
            private TranspositionTable TTable;
            private Move InvalidMove;

            public MoveOrderer(MoveGenerator m, TranspositionTable tTable)
            {
                moveEvals = new int[maxMoveCount];
                moveGen = m;
                TTable = tTable;
                InvalidMove = Move.Invalid;
            }

            public void OrderMoves(Board board, List<Move> moves, bool useTT)
            {
                Move hashMove = InvalidMove;
                if (useTT)
                {
                    hashMove = TTable.GetStoredMove();
                }

                for (var i = 0; i < moves.Count; i++)
                {
                    var eval = 0;
                    var pieceType = Piece.typeMask & board.BoardArray[moves[i].From];
                    var capPieceType = Piece.typeMask & board.BoardArray[moves[i].To];

                    if (capPieceType != Piece.Empty)
                    {
                        eval += CaptureValueMultiplier;
                    }

                    if (moves[i].Promotion != Piece.Empty)
                    {
                        eval += PieceValue(moves[i].Promotion);
                    }
                    else
                    {
                        if (((moveGen.OpponentPawnAttackMap >> moves[i].To) & 1) != 0)
                        {
                            eval -= IndexSeenByOpponentPawnPenalty;
                        }
                    }

                    if (Move.SameMove(moves[i], hashMove))
                    {
                        eval += 10000;
                    }

                    moveEvals[i] = eval;
                }

                Sort(moves);
            }

            private void Sort(List<Move> moves)
            {
                for (int i = 0; i < moves.Count - 1; i++)
                {
                    for (int j = i + 1; j > 0; j--)
                    {
                        int swapIndex = j - 1;
                        if (moveEvals[swapIndex] < moveEvals[j])
                        {
                            (moves[j], moves[swapIndex]) = (moves[swapIndex], moves[j]);
                            (moveEvals[j], moveEvals[swapIndex]) = (moveEvals[swapIndex], moveEvals[j]);
                        }
                    }
                }
                //var result = moves.OrderByDescending(x => x.Eval).ToList();
                //moves = result;
            }

            static int PieceValue(int piece)
            {
                piece = piece & Piece.typeMask;

                switch (piece)
                {
                    case Piece.Queen:
                        return BoardEval.Queen;
                    case Piece.Rook:
                        return BoardEval.Rook;
                    case Piece.Knight:
                        return BoardEval.Knight;
                    case Piece.Bishop:
                        return BoardEval.Bishop;
                    case Piece.Pawn:
                        return BoardEval.Pawn;
                    default:
                        return 0;
                }
            }
        }

        internal class TranspositionTable
        {
            /**
         * INFO:
         * This internal class is an altered version from Sebastian Lague's Chess-AI Project
         * which can be found here: https://github.com/SebLague/Chess-AI/
         * Adjustments were made to fit into this program, license permitting.
         */
            public const int Failed = int.MinValue;
            public const int Exact = 0;
            public const int LowerBound = 1;
            public const int UpperBound = 2;

            public Entry[] Entries;
            public readonly ulong Size;
            public bool Enabled = true;

            private Board Board;

            public TranspositionTable(Board board, int size)
            {
                Board = board;
                Size = (ulong)size;

                Entries = new Entry[size];
            }

            public void Clear()
            {
                for (var i = 0; i < Entries.Length; i++)
                {
                    Entries[i] = new Entry();
                }
            }

            public ulong Index
            {
                get { return Board.ZobristKey % Size; }
            }

            public Move GetStoredMove()
            {
                return Entries[Index].Move;
            }

            public int LookupEval(int depth, int ply, int alpha, int beta)
            {
                if (!Enabled)
                {
                    return Failed;
                }

                var e = Entries[Index];
                if (e.Key == Board.ZobristKey)
                {
                    if (e.Depth >= depth)
                    {
                        var correctedScore = MateEval(e.Value, ply);

                        if (e.NodeType == Exact)
                        {
                            return correctedScore;
                        }

                        if (e.NodeType == UpperBound && correctedScore <= alpha)
                        {
                            return correctedScore;
                        }

                        if (e.NodeType == LowerBound && correctedScore >= beta)
                        {
                            return correctedScore;
                        }
                    }
                }

                return Failed;
            }

            public void StoreEval(int depth, int ply, int eval, int evalType, Move move)
            {
                if (!Enabled)
                {
                    return;
                }

                var e = new Entry(Board.ZobristKey, StorageMateEval(eval, ply), (byte)depth, (byte)evalType, move);
                Entries[Index] = e;
            }

            private int StorageMateEval(int eval, int ply)
            {
                if (Math.Abs(eval) > Search.MateScore - 1000)
                {
                    var sign = Math.Sign(eval);
                    return (eval * sign + ply) * sign;
                }

                return eval;
            }

            private int MateEval(int eval, int ply)
            {
                if (Math.Abs(eval) > Search.MateScore - 1000)
                {
                    int sign = Math.Sign(eval);
                    return (eval * sign - ply) * sign;
                }

                return eval;
            }

            public readonly struct Entry
            {
                public readonly ulong Key;
                public readonly int Value;
                public readonly byte Depth;
                public readonly byte NodeType;
                public readonly Move Move;

                public Entry(ulong key, int value, byte depth, byte nodeType, Move move)
                {
                    Key = key;
                    Value = value;
                    Depth = depth;
                    NodeType = nodeType;
                    Move = move;
                }

                public static int GetSize()
                {
                    return System.Runtime.InteropServices.Marshal.SizeOf<Entry>();
                }
            }
        }

        internal class Book
        {
            public Move FindMove(Board board, List<Move> possibleMoves)
            {
                // White
                if (board.Fen.IsWhiteToMove)
                {
                    switch (board.Fen.FullMoves)
                    {
                        // Play king's pawn opening (e4).
                        case 1:
                            return ParseMove(board, possibleMoves, "e4");
                        case 2:
                            return ParseMove(board, possibleMoves, "g1f3");
                        case 3:
                            return ParseMove(board, possibleMoves, "d4");
                    }
                }
                // Black
                else
                {
                    switch (board.Fen.FullMoves)
                    {
                        // Play Sicilian defense (c5).
                        case 1:
                            return ParseMove(board, possibleMoves, "c5");
                        case 2:
                            return ParseMove(board, possibleMoves, "d6");
                        case 3:
                            return ParseMove(board, possibleMoves, "b8c6");
                    }
                }

                return Move.Invalid;
            }

            private const string StartPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR";
            private const string KingsPawnOpening = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR";
            private const string SicilianDefense = "rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR";
            private const string SicilianDefense_Nf3 = "rnbqkbnr/pp1ppppp/8/2p5/4P3/5N2/PPPP1PPP/RNBQKB1R";
        }
    }

    internal class BoardEval
    {
        /**
         * INFO:
         * This internal class is an altered version from Sebastian Lague's Chess-AI Project
         * which can be found here: https://github.com/SebLague/Chess-AI/
         * Adjustments were made to fit into this program, license permitting.
         */
        public const int Pawn = 100;
        public const int Knight = 300;
        public const int Bishop = 320;
        public const int Rook = 500;
        public const int Queen = 900;

        private const float endGameStart = Rook * 2 + Bishop + Knight;

        private Board Board;

        public int Eval(Board board)
        {
            Board = board;

            var whiteEval = 0;
            var blackEval = 0;

            var whiteMats = Material(Board.WhiteIndex);
            var blackMats = Material(Board.BlackIndex);

            var whiteMatsNoPawns = whiteMats - Board.Pawns[Board.WhiteIndex].Count * Pawn;
            var blackMatsNoPawns = blackMats - Board.Pawns[Board.BlackIndex].Count * Pawn;

            var whiteEndGame = EndGame(whiteMatsNoPawns);
            var blackEndGame = EndGame(blackMatsNoPawns);

            whiteEval += whiteMats;
            blackEval += blackMats;

            whiteEval += MopUp(Board.WhiteIndex, Board.BlackIndex, whiteMats, blackMats, blackEndGame);
            blackEval += MopUp(Board.BlackIndex, Board.WhiteIndex, blackMats, whiteMats, whiteEndGame);

            whiteEval += EvalTables(Board.WhiteIndex, blackEndGame);
            blackEval += EvalTables(Board.BlackIndex, whiteEndGame);

            return (whiteEval - blackEval) * (Board.Fen.IsWhiteToMove ? 1 : -1);
        }

        private int Material(int colourIndex)
        {
            var mats = 0;
            mats += Board.Pawns[colourIndex].Count * Pawn;
            mats += Board.Knights[colourIndex].Count * Knight;
            mats += Board.Bishops[colourIndex].Count * Bishop;
            mats += Board.Rooks[colourIndex].Count * Rook;
            mats += Board.Queens[colourIndex].Count * Queen;

            return mats;
        }

        private float EndGame(int matsNoPawns)
        {
            const float multiplier = 1 / endGameStart;
            return 1 - Math.Min(1, matsNoPawns * multiplier);
        }

        private int MopUp(int friendlyIndex, int opponentIndex, int friendlyMats, int opponentMats, float endGame)
        {
            var eval = 0;
            if (friendlyMats > opponentMats + Pawn * 2 && endGame > 0)
            {
                eval += PrecomputedMoveData.CentreManhattanDistance[Board.Kings[opponentIndex]] * 10;
                eval += (14 -
                          PrecomputedMoveData.NumRookMovesToReachSquare(Board.Kings[friendlyIndex],
                              Board.Kings[opponentIndex])) * 4;
                return (int)(eval * endGame);
            }

            return 0;
        }

        private int EvalTables(int colourIndex, float endGame)
        {
            var eval = 0;
            var isWhite = colourIndex == Board.WhiteIndex;

            eval += EvalTable(Tables.Pawns, Board.Pawns[colourIndex], isWhite);
            eval += EvalTable(Tables.Knights, Board.Knights[colourIndex], isWhite);
            eval += EvalTable(Tables.Bishops, Board.Bishops[colourIndex], isWhite);
            eval += EvalTable(Tables.Rooks, Board.Rooks[colourIndex], isWhite);
            eval += EvalTable(Tables.Queens, Board.Queens[colourIndex], isWhite);

            var middleKing = Tables.Read(Tables.KingMiddle, Board.Kings[colourIndex], isWhite);
            eval += (int)(middleKing * (1 - endGame));

            var endKing = Tables.Read(Tables.KingEnd, Board.Kings[colourIndex], isWhite);
            eval += (int)(endKing * (endGame));

            return eval;
        }

        private static int EvalTable(int[] table, PieceList pieces, bool isWhite)
        {
            var eval = 0;
            for (var i = 0; i < pieces.Count; i++)
            {
                eval += Tables.Read(table, pieces[i], isWhite);
            }

            return eval;
        }

        internal static class Tables
        {
            /**
         * INFO:
         * This internal class is an altered version from Sebastian Lague's Chess-AI Project
         * which can be found here: https://github.com/SebLague/Chess-AI/
         * Adjustments were made to fit into this program, license permitting.
         */
            public static int Read(int[] table, int index, bool isWhite)
            {
                if (isWhite)
                {
                    int file = Board.FileIndex(index);
                    int rank = Board.RankIndex(index);

                    rank = 7 - rank;
                    index = rank * 8 + file;
                }

                return table[index];
            }

            public static readonly int[] Pawns =
            {
                0, 0, 0, 0, 0, 0, 0, 0,
                50, 50, 50, 50, 50, 50, 50, 50,
                10, 10, 20, 30, 30, 20, 10, 10,
                5, 5, 10, 25, 25, 10, 5, 5,
                0, 0, 0, 20, 20, 0, 0, 0,
                5, -5, -10, 0, 0, -10, -5, 5,
                5, 10, 10, -20, -20, 10, 10, 5,
                0, 0, 0, 0, 0, 0, 0, 0,
            };

            public static readonly int[] Knights =
            {
                -50, -40, -30, -30, -30, -30, -40, -50,
                -40, -20, 0, 0, 0, 0, -20, -40,
                -30, 0, 10, 15, 15, 10, 0, -30,
                -30, 5, 15, 20, 20, 15, 5, -30,
                -30, 0, 15, 20, 20, 15, 0, -30,
                -30, 5, 10, 15, 15, 10, 5, -30,
                -40, -20, 0, 5, 5, 0, -20, -40,
                -50, -40, -30, -30, -30, -30, -40, -50,
            };

            public static readonly int[] Bishops =
            {
                -20, -10, -10, -10, -10, -10, -10, -20,
                -10, 0, 0, 0, 0, 0, 0, -10,
                -10, 0, 5, 10, 10, 5, 0, -10,
                -10, 5, 5, 10, 10, 5, 5, -10,
                -10, 0, 10, 10, 10, 10, 0, -10,
                -10, 10, 10, 10, 10, 10, 10, -10,
                -10, 5, 0, 0, 0, 0, 5, -10,
                -20, -10, -10, -10, -10, -10, -10, -20,
            };

            public static readonly int[] Rooks =
            {
                0, 0, 0, 0, 0, 0, 0, 0,
                5, 10, 10, 10, 10, 10, 10, 5,
                -5, 0, 0, 0, 0, 0, 0, -5,
                -5, 0, 0, 0, 0, 0, 0, -5,
                -5, 0, 0, 0, 0, 0, 0, -5,
                -5, 0, 0, 0, 0, 0, 0, -5,
                -5, 0, 0, 0, 0, 0, 0, -5,
                0, 0, 0, 5, 5, 0, 0, 0
            };

            public static readonly int[] Queens =
            {
                -20, -10, -10, -5, -5, -10, -10, -20,
                -10, 0, 0, 0, 0, 0, 0, -10,
                -10, 0, 5, 5, 5, 5, 0, -10,
                -5, 0, 5, 5, 5, 5, 0, -5,
                0, 0, 5, 5, 5, 5, 0, -5,
                -10, 5, 5, 5, 5, 5, 0, -10,
                -10, 0, 5, 0, 0, 0, 0, -10,
                -20, -10, -10, -5, -5, -10, -10, -20
            };

            public static readonly int[] KingMiddle =
            {
                -30, -40, -40, -50, -50, -40, -40, -30,
                -30, -40, -40, -50, -50, -40, -40, -30,
                -30, -40, -40, -50, -50, -40, -40, -30,
                -30, -40, -40, -50, -50, -40, -40, -30,
                -20, -30, -30, -40, -40, -30, -30, -20,
                -10, -20, -20, -20, -20, -20, -20, -10,
                20, 20, 0, 0, 0, 0, 20, 20,
                20, 30, 10, 0, 0, 10, 30, 20
            };

            public static readonly int[] KingEnd =
            {
                -50, -40, -30, -20, -20, -30, -40, -50,
                -30, -20, -10, 0, 0, -10, -20, -30,
                -30, -10, 20, 30, 30, 20, -10, -30,
                -30, -10, 30, 40, 40, 30, -10, -30,
                -30, -10, 30, 40, 40, 30, -10, -30,
                -30, -10, 20, 30, 30, 20, -10, -30,
                -30, -30, 0, 0, 0, 0, -30, -30,
                -50, -30, -30, -30, -30, -30, -30, -50
            };
        }
    }

    internal struct Fen
    {
        public string Pieces;
        public bool IsWhiteToMove;
        public string CastlingRights;
        public string EnPassant;
        public int HalfMoves;
        public int FullMoves;

        public Fen(string fen = Board.StartPosition)
        {
            Pieces = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR";
            IsWhiteToMove = true;
            CastlingRights = "KQkq";
            EnPassant = "-";
            HalfMoves = 0;
            FullMoves = 1;

            if (fen == Board.StartPosition)
            {
                return;
            }

            var fenArray = fen.Split(' ');
            try
            {
                Pieces = fenArray[0];
                IsWhiteToMove = fenArray[1] == "w";
                CastlingRights = fenArray[2];
                EnPassant = fenArray[3];
                HalfMoves = int.Parse(fenArray[4]);
                FullMoves = int.Parse(fenArray[5]);
            }
            catch (Exception e)
            {
                ChessGame.StateFile.WriteLine("Failed to parse Fen");
                ChessGame.StateFile.WriteLine(e.Message);
            }
        }

        public new string ToString()
        {
            return $"{Pieces} {(IsWhiteToMove ? "w" : "b")} {CastlingRights} {EnPassant} {HalfMoves} {FullMoves}";
        }
    }
    
    internal static class Zobrist
    {
        /**
         * INFO:
         * This internal class is an altered version from Sebastian Lague's Chess-AI Project
         * which can be found here: https://github.com/SebLague/Chess-AI/
         * Adjustments were made to fit into this program, license permitting.
         */
        private const int Seed = 6521934;
        private const string fileName = ".\\RandomNumbers.txt";

        // Piece type, colour, index.
        public static readonly ulong[,,] PiecesArray = new ulong[8, 2, 64];
        public static readonly ulong[] CastlingRights = new ulong[16];

        public static readonly ulong[] EpFile = new ulong[9];
        public static readonly ulong ColourToMove;

        private static Random Random = new Random(Seed);

        static Zobrist()
        {
            try
            {
                var randomNums = ReadRandomNumbers();

                for (var index = 0; index < 64; index++)
                {
                    for (var piece = 0; piece < 8; piece++)
                    {
                        PiecesArray[piece, Board.WhiteIndex, index] = randomNums.Dequeue();
                        PiecesArray[piece, Board.BlackIndex, index] = randomNums.Dequeue();
                    }
                }

                for (var i = 0; i < 16; i++)
                {
                    CastlingRights[i] = randomNums.Dequeue();
                }

                for (var i = 0; i < EpFile.Length; i++)
                {
                    EpFile[i] = randomNums.Dequeue();
                }

                ColourToMove = randomNums.Dequeue();
            }
            catch (Exception e)
            {
                ChessGame.StateFile.WriteLine("Zobrist Error");
                ChessGame.StateFile.WriteLine(e.Message);
            }
        }

        public static ulong CalcZobristKey(Board board)
        {
            ulong key = 0;

            for (var index = 0; index < 64; index++)
            {
                if (board.BoardArray[index] != Piece.Empty)
                {
                    var pieceType = board.BoardArray[index] & Piece.typeMask;
                    var pieceColour = board.BoardArray[index] & Piece.colourMask;

                    key ^= PiecesArray[pieceType, (pieceColour == Piece.White) ? Board.WhiteIndex : Board.BlackIndex,
                        index];
                }
            }

            try
            {
                var ep = board.Fen.EnPassant;
                if (ep != "-")
                {
                    var epFile = Board.Files.IndexOf(ep[0]) + 1;
                    key ^= EpFile[epFile];
                }
            }
            catch (Exception e)
            {
                ChessGame.StateFile.WriteLine("Cannot parse ep for ZobristKey");
                ChessGame.StateFile.WriteLine(e.Message);
            }

            if (!board.Fen.IsWhiteToMove)
            {
                key ^= ColourToMove;
            }

            try
            {
                var castles = board.Fen.CastlingRights;
                var whiteCanCastleKingSide = castles.Contains('K') ? 0b1000 : 0;
                var whiteCanCastleQueenSide = castles.Contains('Q') ? 0b100 : 0;
                var BlackCanCastleKingSide = castles.Contains('k') ? 0b10 : 0;
                var BlackCanCastleQueenSide = castles.Contains('q') ? 0b1 : 0;
                key ^= CastlingRights[whiteCanCastleKingSide |
                                      whiteCanCastleQueenSide |
                                      BlackCanCastleKingSide |
                                      BlackCanCastleQueenSide];
            }
            catch (Exception e)
            {
                ChessGame.StateFile.WriteLine("Cannot parse castling rights for ZobristKey");
                ChessGame.StateFile.WriteLine(e.Message);
            }

            return key;
        }

        private static void WriteRandomNumbers()
        {
            Random = new Random(Seed);
            var randomString = string.Empty;
            var count = 64 * 8 * 2 + CastlingRights.Length + EpFile.Length + 1;

            for (var i = 0; i < count; i++)
            {
                randomString += RandUlong();
                if (i != count - 1)
                {
                    randomString += ",";
                }
            }

            var writer = new StreamWriter(fileName);
            writer.Write(randomString);
            writer.Close();
        }

        private static Queue<ulong> ReadRandomNumbers()
        {
            if (!File.Exists(fileName))
            {
                WriteRandomNumbers();
            }

            var randomNumbers = new Queue<ulong>();

            var reader = new StreamReader(fileName);
            var numbersString = reader.ReadToEnd();
            reader.Close();

            var numberStrings = numbersString.Split(',');
            for (var i = 0; i < numberStrings.Length; i++)
            {
                try
                {
                    var num = ulong.Parse(numberStrings[i]);
                    randomNumbers.Enqueue(num);
                }
                catch (Exception e)
                {
                    ChessGame.StateFile.WriteLine("Random Number parse failed");
                    ChessGame.StateFile.WriteLine(e.Message);
                }
            }

            return randomNumbers;
        }

        private static ulong RandUlong()
        {
            var buffer = new byte[8];
            Random.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer, 0);
        }
    }
}
