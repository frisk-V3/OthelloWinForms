using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace OthelloWinForms
{
    public enum Piece { Empty = 0, Black, White }
    public enum Phase { Title, SelectSide, PlayerTurn, CPUTurn, InvalidMove, GameOver }

    public class MainForm : Form
    {
        // ───────── 定数 ─────────
        const int BOARD = 8;
        const float CELL = 72f;
        const float MARGIN = 40f;
        const float UI_HEIGHT = 110f;

        // ───────── ゲーム状態 ─────────
        Phase phase = Phase.Title;
        Piece[,] board = new Piece[BOARD, BOARD];
        Piece playerColor = Piece.Black;
        Piece cpuColor = Piece.White;

        bool firstClick = false;
        int fc_r = -1, fc_c = -1;

        string message = "";
        float msgTimer = 0f;

        Timer frameTimer;
        Timer cpuTimer;
        bool cpuPending = false;

        Font titleFont;
        Font uiFont;
        Font msgFont;

        // ───────── 重み（評価関数）─────────
        readonly int[,] WEIGHT = {
            {120,-20,20, 5, 5,20,-20,120},
            {-20,-40,-5,-5,-5,-5,-40,-20},
            { 20, -5,15, 3, 3,15, -5, 20},
            {  5, -5, 3, 3, 3, 3, -5,  5},
            {  5, -5, 3, 3, 3, 3, -5,  5},
            { 20, -5,15, 3, 3,15, -5, 20},
            {-20,-40,-5,-5,-5,-5,-40,-20},
            {120,-20,20, 5, 5,20,-20,120}
        };

        readonly int[] DX = { -1,-1, 0, 1, 1, 1, 0,-1 };
        readonly int[] DY = {  0,-1,-1,-1, 0, 1, 1, 1 };

        // ───────── コンストラクタ ─────────
        public MainForm()
        {
            DoubleBuffered = true;
            Text = "Othello vs CPU (WinForms)";
            Width = (int)(MARGIN * 2 + CELL * BOARD) + 16;
            Height = (int)(MARGIN * 2 + CELL * BOARD + UI_HEIGHT) + 39;

            titleFont = new Font("Meiryo UI", 24f, FontStyle.Bold);
            uiFont = new Font("Meiryo UI", 12f);
            msgFont = new Font("Meiryo UI", 14f, FontStyle.Bold);

            InitBoard();

            frameTimer = new Timer { Interval = 16 };
            frameTimer.Tick += (s, e) =>
            {
                if (msgTimer > 0)
                {
                    msgTimer -= 0.016f;
                    if (msgTimer < 0) msgTimer = 0;
                }
                Invalidate();
            };
            frameTimer.Start();

            cpuTimer = new Timer { Interval = 800 };
            cpuTimer.Tick += CpuTimer_Tick;

            MouseDown += MainForm_MouseDown;
            KeyDown += MainForm_KeyDown;
        }

        // ───────── 盤面初期化 ─────────
        void InitBoard()
        {
            for (int r = 0; r < BOARD; r++)
                for (int c = 0; c < BOARD; c++)
                    board[r, c] = Piece.Empty;

            board[3, 3] = Piece.White;
            board[3, 4] = Piece.Black;
            board[4, 3] = Piece.Black;
            board[4, 4] = Piece.White;
        }

        void StartGame()
        {
            InitBoard();
            phase = Phase.PlayerTurn;
            firstClick = false;
            message = "";
            msgTimer = 0f;
        }

        // ───────── 入力処理（キー）─────────
        void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                if (phase == Phase.Title) phase = Phase.SelectSide;
                else if (phase == Phase.InvalidMove)
                {
                    phase = Phase.PlayerTurn;
                    firstClick = false;
                    message = "";
                }
                else if (phase == Phase.GameOver) phase = Phase.Title;
            }

            if (phase == Phase.SelectSide)
            {
                if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1)
                {
                    playerColor = Piece.Black;
                    cpuColor = Piece.White;
                    StartGame();
                }
                else if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2)
                {
                    playerColor = Piece.White;
                    cpuColor = Piece.Black;
                    StartGame();
                    StartCpuTurn();
                }
            }
        }

        // ───────── 入力処理（マウス）─────────
        void MainForm_MouseDown(object sender, MouseEventArgs e)
        {
            float mx = e.X;
            float my = e.Y;

            if (phase == Phase.SelectSide)
            {
                float winW = MARGIN * 2 + CELL * BOARD;
                RectangleF btn1 = new RectangleF(winW / 2 - 130, 240, 120, 50);
                RectangleF btn2 = new RectangleF(winW / 2 + 10, 240, 120, 50);

                if (btn1.Contains(mx, my))
                {
                    playerColor = Piece.Black;
                    cpuColor = Piece.White;
                    StartGame();
                }
                else if (btn2.Contains(mx, my))
                {
                    playerColor = Piece.White;
                    cpuColor = Piece.Black;
                    StartGame();
                    StartCpuTurn();
                }
                return;
            }

            if (phase == Phase.PlayerTurn && e.Button == MouseButtons.Left)
            {
                int c = (int)((mx - MARGIN) / CELL);
                int r = (int)((my - MARGIN) / CELL);

                if (!InBounds(r, c)) return;

                if (!firstClick)
                {
                    if (CanPlace(board, r, c, playerColor))
                    {
                        firstClick = true;
                        fc_r = r;
                        fc_c = c;
                        message = "確定するにはもう一度クリック";
                    }
                    else
                    {
                        message = "できません";
                        msgTimer = 2f;
                        phase = Phase.InvalidMove;
                    }
                }
                else
                {
                    if (r == fc_r && c == fc_c)
                    {
                        board = ApplyMove(board, r, c, playerColor);
                        firstClick = false;
                        message = "";

                        if (HasMove(board, cpuColor))
                        {
                            StartCpuTurn();
                        }
                        else if (HasMove(board, playerColor))
                        {
                            message = "CPUはパスします";
                            msgTimer = 2f;
                        }
                        else
                        {
                            phase = Phase.GameOver;
                        }
                    }
                    else if (CanPlace(board, r, c, playerColor))
                    {
                        fc_r = r;
                        fc_c = c;
                        message = "確定するにはもう一度クリック";
                    }
                    else
                    {
                        message = "できません";
                        msgTimer = 2f;
                        phase = Phase.InvalidMove;
                        firstClick = false;
                    }
                }
            }
        }

        // ───────── CPU 手番 ─────────
        void StartCpuTurn()
        {
            phase = Phase.CPUTurn;
            cpuPending = true;
            cpuTimer.Stop();
            cpuTimer.Start();
        }

        void CpuTimer_Tick(object sender, EventArgs e)
        {
            cpuTimer.Stop();
            if (!cpuPending) return;
            cpuPending = false;

            var mv = BestMove(board, cpuColor);
            if (mv.r >= 0)
            {
                board = ApplyMove(board, mv.r, mv.c, cpuColor);
            }

            if (HasMove(board, playerColor))
            {
                phase = Phase.PlayerTurn;
            }
            else if (HasMove(board, cpuColor))
            {
                message = "あなたはパスです";
                msgTimer = 2f;
                StartCpuTurn();
            }
            else
            {
                phase = Phase.GameOver;
            }
        }

        // ───────── ロジック（C++版と同じ）─────────
        bool InBounds(int r, int c)
            => r >= 0 && r < BOARD && c >= 0 && c < BOARD;

        List<(int r, int c)> Flippable(Piece[,] b, int r, int c, Piece me)
        {
            var result = new List<(int, int)>();
            Piece opp = (me == Piece.Black) ? Piece.White : Piece.Black;

            for (int d = 0; d < 8; d++)
            {
                var line = new List<(int, int)>();
                int nr = r + DY[d], nc = c + DX[d];

                while (InBounds(nr, nc) && b[nr, nc] == opp)
                {
                    line.Add((nr, nc));
                    nr += DY[d];
                    nc += DX[d];
                }

                if (line.Count > 0 && InBounds(nr, nc) && b[nr, nc] == me)
                    result.AddRange(line);
            }
            return result;
        }

        bool CanPlace(Piece[,] b, int r, int c, Piece me)
            => b[r, c] == Piece.Empty && Flippable(b, r, c, me).Count > 0;

        bool HasMove(Piece[,] b, Piece me)
        {
            for (int r = 0; r < BOARD; r++)
                for (int c = 0; c < BOARD; c++)
                    if (CanPlace(b, r, c, me))
                        return true;
            return false;
        }

        Piece[,] ApplyMove(Piece[,] b, int r, int c, Piece me)
        {
            var nb = (Piece[,])b.Clone();
            nb[r, c] = me;

            foreach (var p in Flippable(b, r, c, me))
                nb[p.r, p.c] = me;

            return nb;
        }

        int CountPiece(Piece[,] b, Piece me)
        {
            int n = 0;
            for (int r = 0; r < BOARD; r++)
                for (int c = 0; c < BOARD; c++)
                    if (b[r, c] == me) n++;
            return n;
        }

        int Evaluate(Piece[,] b, Piece me)
        {
            Piece opp = (me == Piece.Black) ? Piece.White : Piece.Black;
            int score = 0;

            for (int r = 0; r < BOARD; r++)
                for (int c = 0; c < BOARD; c++)
                {
                    if (b[r, c] == me) score += WEIGHT[r, c];
                    if (b[r, c] == opp) score -= WEIGHT[r, c];
                }
            return score;
        }

        int Minimax(Piece[,] b, int depth, bool maximizing, Piece me, int alpha, int beta)
        {
            Piece cur = maximizing ? me : (me == Piece.Black ? Piece.White : Piece.Black);

            if (depth == 0 || (!HasMove(b, Piece.Black) && !HasMove(b, Piece.White)))
                return Evaluate(b, me);

            if (!HasMove(b, cur))
                return Minimax(b, depth - 1, !maximizing, me, alpha, beta);

            if (maximizing)
            {
                int best = int.MinValue;

                for (int r = 0; r < BOARD; r++)
                    for (int c = 0; c < BOARD; c++)
                    {
                        if (!CanPlace(b, r, c, cur)) continue;

                        int val = Minimax(ApplyMove(b, r, c, cur), depth - 1, false, me, alpha, beta);
                        best = Math.Max(best, val);
                        alpha = Math.Max(alpha, val);

                        if (beta <= alpha) return best;
                    }
                return best;
            }
            else
            {
                int best = int.MaxValue;

                for (int r = 0; r < BOARD; r++)
                    for (int c = 0; c < BOARD; c++)
                    {
                        if (!CanPlace(b, r, c, cur)) continue;

                        int val = Minimax(ApplyMove(b, r, c, cur), depth - 1, true, me, alpha, beta);
                        best = Math.Min(best, val);
                        beta = Math.Min(beta, val);

                        if (beta <= alpha) return best;
                    }
                return best;
            }
        }

        (int r, int c) BestMove(Piece[,] b, Piece cpu)
        {
            int best = int.MinValue;
            (int r, int c) move = (-1, -1);

            for (int r = 0; r < BOARD; r++)
                for (int c = 0; c < BOARD; c++)
                {
                    if (!CanPlace(b, r, c, cpu)) continue;

                    int val = Minimax(ApplyMove(b, r, c, cpu), 2, false, cpu, int.MinValue, int.MaxValue);
                    if (val > best)
                    {
                        best = val;
                        move = (r, c);
                    }
                }
            return move;
        }

        // ───────── 描画 ─────────
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(Color.FromArgb(30, 30, 30));

            float winW = MARGIN * 2 + CELL * BOARD;
            float winH = MARGIN * 2 + CELL * BOARD + UI_HEIGHT;

            if (phase == Phase.Title)
            {
                DrawTitle(g, winW, winH);
            }
            else if (phase == Phase.SelectSide)
            {
                DrawSelectSide(g, winW, winH);
            }
            else
            {
                DrawBoardAndUI(g, winW, winH);
            }
        }

        void DrawTitle(Graphics g, float winW, float winH)
        {
            string t1 = "OTHELLO vs CPU";
            string t2 = "Press SPACE to Start";

            var size1 = g.MeasureString(t1, titleFont);
            var size2 = g.MeasureString(t2, uiFont);

            g.DrawString(t1, titleFont, Brushes.Khaki,
                winW / 2 - size1.Width / 2, winH / 2 - 80);
            g.DrawString(t2, uiFont, Brushes.Gainsboro,
                winW / 2 - size2.Width / 2, winH / 2 + 10);
        }

        void DrawSelectSide(Graphics g, float winW, float winH)
        {
            string t1 = "先攻・後攻を選んでください";
            var size = g.MeasureString(t1, titleFont);
            g.DrawString(t1, titleFont, Brushes.Khaki,
                winW / 2 - size.Width / 2, 160);

            RectangleF b1 = new RectangleF(winW / 2 - 130, 240, 120, 50);
            RectangleF b2 = new RectangleF(winW / 2 + 10, 240, 120, 50);

            using (var pen = new Pen(Color.Gainsboro, 2))
            using (var brush = new SolidBrush(Color.FromArgb(50, 50, 50)))
            {
                g.FillRectangle(brush, b1);
                g.DrawRectangle(pen, b1.X, b1.Y, b1.Width, b1.Height);

                g.FillRectangle(brush, b2);
                g.DrawRectangle(pen, b2.X, b2.Y, b2.Width, b2.Height);
            }

            g.DrawString("先攻(黒)[1]", uiFont, Brushes.White, b1.X + 8, b1.Y + 15);
            g.DrawString("後攻(白)[2]", uiFont, Brushes.White, b2.X + 8, b2.Y + 15);

            string td = "クリックまたは 1/2 キー";
            var s2 = g.MeasureString(td, uiFont);
            g.DrawString(td, uiFont, Brushes.LightGray,
                winW / 2 - s2.Width / 2, 320);
        }

        void DrawBoardAndUI(Graphics g, float winW, float winH)
        {
            // board background
            using (var bgBrush = new SolidBrush(Color.FromArgb(34, 100, 40)))
            {
                g.FillRectangle(bgBrush, MARGIN, MARGIN, CELL * BOARD, CELL * BOARD);
            }

            using (var linePen = new Pen(Color.FromArgb(0, 60, 0), 1))
            {
                for (int i = 0; i <= BOARD; i++)
                {
