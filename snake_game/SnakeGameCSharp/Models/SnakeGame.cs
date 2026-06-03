namespace SnakeGameCSharp.Models;

public enum GameState { Waiting, Running, Paused, Over }

public class SnakeGame
{
    public const int Cols = 20, Rows = 20;

    public List<Point> Snake          { get; private set; } = [];
    public List<Point> Foods          { get; private set; } = [];
    public Point?      SuperFood      { get; private set; }
    public Point       Dir            { get; private set; } = new(1, 0);
    public Point       NextDir        { get; private set; } = new(1, 0);
    public int         Score          { get; private set; }
    public int         Best           { get; private set; }
    public int         FoodEatenCount { get; private set; }
    public int         PendingGrowth  { get; private set; }
    public GameState   State          { get; private set; } = GameState.Waiting;

    public void Init()
    {
        var savedBest = Best;
        Snake         = [new(10, 10), new(9, 10), new(8, 10)];
        Dir = NextDir = new(1, 0);
        Score = FoodEatenCount = PendingGrowth = 0;
        SuperFood     = null;
        Foods         = [];
        State         = GameState.Waiting;
        Best          = savedBest;
        SpawnFood(); SpawnFood();
    }

    public void SetRunning() => State = GameState.Running;

    // Returns false when the snake dies
    public bool Tick()
    {
        if (State != GameState.Running) return true;

        Dir = NextDir;
        var head = new Point(
            (Snake[0].X + Dir.X + Cols) % Cols,
            (Snake[0].Y + Dir.Y + Rows) % Rows);

        if (Snake.Contains(head))
        {
            State = GameState.Over;
            return false;
        }

        Snake.Insert(0, head);

        if (SuperFood == head)
        {
            Score        += 2;
            if (Score > Best) Best = Score;
            SuperFood     = null;
            PendingGrowth += 2;
        }
        else
        {
            var idx = Foods.IndexOf(head);
            if (idx >= 0)
            {
                Score++;
                if (Score > Best) Best = Score;
                Foods.RemoveAt(idx);
                SpawnFood();
                FoodEatenCount++;
                PendingGrowth++;
                if (FoodEatenCount % 2 == 0) SpawnSuperFood();
            }
        }

        if (PendingGrowth > 0) PendingGrowth--;
        else Snake.RemoveAt(Snake.Count - 1);

        return true;
    }

    public void Steer(int x, int y)
    {
        var nd  = new Point(x, y);
        var opp = new Point(-Dir.X, -Dir.Y);
        if (nd != opp) NextDir = nd;
    }

    public void TogglePause()
    {
        if      (State == GameState.Running) State = GameState.Paused;
        else if (State == GameState.Paused)  State = GameState.Running;
    }

    private void SpawnFood()
    {
        Point pos;
        do { pos = new(Random.Shared.Next(Cols), Random.Shared.Next(Rows)); }
        while (Snake.Contains(pos) || Foods.Contains(pos) || pos == SuperFood);
        Foods.Add(pos);
    }

    private void SpawnSuperFood()
    {
        Point pos;
        do { pos = new(Random.Shared.Next(Cols), Random.Shared.Next(Rows)); }
        while (Snake.Contains(pos) || Foods.Contains(pos));
        SuperFood = pos;
    }
}
