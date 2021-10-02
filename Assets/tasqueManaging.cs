using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;
using rnd = UnityEngine.Random;

public class tasqueManaging : MonoBehaviour
{
    public new KMAudio audio;
    public KMBombInfo bomb;
    public KMBombModule module;

    public KMSelectable[] tiles;
    private Renderer[] tileRenders;
    public Renderer[] leds;
    public TextMesh screenText;
    public Material litMat;
    private Material blackMat;
    public Color[] tileColors;

    private int startingPosition;
    private int currentPosition;
    private int[] movableTiles;
    private int[] goalTiles = new int[3];
    private int stage;
    private int[] groups = new int[4];
    private int[][] subtiles = new int[][] { new int[4], new int[4], new int[4], new int[4] };
    private string[] maze;

    private static float waitTime = 15f;
    private static readonly int[][] groupIndices = new int[][]
    {
        new int[] { 0, 2, 4, 1 },
        new int[] { 3, 7, 10, 6 },
        new int[] { 5, 9, 12, 8 },
        new int[] { 11, 14, 15, 13 }
    };
    private static readonly int[][] adjacentTiles = new int[][]
     {
        new int[] { -1, -1, 1, 2 },
        new int[] { -1, 0, 3, 4 },
        new int[] { 0, -1, 4, 5 },
        new int[] { -1, 1, 6, 7 },
        new int[] { 1, 2, 7, 8 },
        new int[] { 2, -1, 8, 9 },
        new int[] { -1, 3, -1, 10 },
        new int[] { 3, 4, 10, 11 },
        new int[] { 4, 5, 11, 12 },
        new int[] { 5, -1, 12, -1 },
        new int[] { 6, 7, -1, 13 },
        new int[] { 7, 8, 13, 14 },
        new int[] { 8, 9, 14, -1 },
        new int[] { 10, 11, -1, 15 },
        new int[] { 11, 12, 15, -1 },
        new int[] { 13, 14, -1, -1 }
    };
    private static readonly string[] mazes = new string[]
    {
        "2;123;23;12;01;03;13;2;23;02;01;13;01;3;02;01",
        "23;13;03;2;0;023;13;23;13;0;01;02;02;13;12;01",
        "23;13;0;23;02;23;13;01;1;02;03;23;12;01;012;1",
        "23;12;02;13;13;23;3;02;01;02;013;3;1;03;02;01"
    };

    private Coroutine countUp;
    private bool bombStarted;
    private bool animating;
    private bool moduleActive;
#pragma warning disable 0649
    private bool TwitchPlaysActive;
#pragma warning restore 0649
    private int startingTime;

    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved;

    private void Awake()
    {
        moduleId = moduleIdCounter++;
        module.OnActivate += delegate () { bombStarted = true; if (TwitchPlaysActive) { waitTime = 30f; } };
        tileRenders = tiles.Select(x => x.GetComponent<Renderer>()).ToArray();
        blackMat = leds[0].material;
        foreach (KMSelectable tile in tiles)
        {
            var ix = Array.IndexOf(tiles, tile);
            tile.OnInteract += delegate () { PressTile(tile); return false; };
            tile.OnHighlight += delegate ()
            {
                if (moduleSolved || !bombStarted || animating)
                    return;
                else if (ix == currentPosition)
                    tileRenders[ix].material.color = tileColors[3];
                else if (movableTiles.Contains(ix))
                    tileRenders[ix].material.color = tileColors[2];
            };
            tile.OnHighlightEnded += delegate ()
            {
                if (moduleSolved || !bombStarted || animating)
                    return;
                else if (ix == currentPosition)
                    tileRenders[ix].material.color = tileColors[1];
                else if (movableTiles.Contains(ix))
                    tileRenders[ix].material.color = tileColors[0];
            };
        }
    }

    private void Start()
    {
        startingTime = (int)bomb.GetTime();
        maze = mazes[bomb.GetSerialNumberNumbers().First() % 4].Split(';').ToArray();

        startingPosition = rnd.Range(0, 16);
        currentPosition = startingPosition;
        Debug.LogFormat("[Tasque Managing #{0}] We begin at {1}, {1}!", moduleId, PositionName(startingPosition));
        tileRenders[startingPosition].material.color = tileColors[1];
        movableTiles = adjacentTiles[startingPosition].ToArray();
        do
        {
            for (int i = 0; i < 3; i++)
                goalTiles[i] = rnd.Range(0, 16);
        }
        while (goalTiles.Any(x => x == startingPosition) || goalTiles.Distinct().Count() != 3);

        for (int i = 0; i < 4; i++)
            if (groupIndices[i].Contains(startingPosition))
                groups[0] = bomb.GetSerialNumberLetters().Any(x => "AEIOU".Contains(x)) ? i : 3 - i;
        if (bomb.GetPortCount() == 0)
            groups[1] = 3 - groups[0];
        else
        {
            var directionOrder = "ULRD";
            var lists = "ULDR;URDL;DULR;RLUD:LRDU;DRUL".Split(';').ToArray();
            var ports = new Port[] { Port.Parallel, Port.Serial, Port.StereoRCA, Port.PS2, Port.DVI, Port.RJ45 };
            var listIx = Array.IndexOf(ports, ports.First(x => bomb.IsPortPresent(x)));
            var directionIndices = lists[listIx].Select(x => directionOrder.IndexOf(x)).ToArray();
            groups[1] = startingTime % 2 == 0 ? directionIndices.Where(x => x != groups[0]).First() : directionIndices.Where(x => x != groups[0]).Last();
        }
        var adjacentIndices = new int[][] { new int[] { 0, 1 }, new int[] { 2, 0 }, new int[] { 1, 3 }, new int[] { 3, 2 } }; // GREEN IS SECOND
        if (adjacentIndices.Any(x => !x.Contains(groups[0]) && !x.Contains(groups[1])))
        {
            var modules = new string[][]
            {
                new string[] { "Simon Screams", "Piragua", "The Sun", "The Hyperlink", "Simon Stores", "Amnesia", "Chilli Beans", "Purple Arrows", "Addition", "Interpunct", "Hexiom" },
                new string[] { "Polyhedral Maze", "Scavenger Hunt", "The Jewel Vault", "Jack Attack", "Ordered Keys", "Infinite Loop", "Metamem", "Organization", "Bowling", "Ladders", "7" },
                new string[] { "Tic Tac Toe", "Shell Game", "Algebra", "Logic Chess", "Not X01", "Mazery", "One-Line", "Simon Selects", "Negativity", "Newline", "Simon Stages" },
                new string[] { "Wire Placement", "Loopover", "Blockbusters", "Spelling Bee", "Jumble Cycle", "UNO!", "Synesthesia", "Masyu", "A Message", "Superparsing", "❖" }
            };
            var ix = 0;
            for (int i = 0; i < 4; i++)
                if (!adjacentIndices[i].Contains(groups[0]) && !adjacentIndices[i].Contains(groups[1]))
                    ix = i;
            groups[2] = modules[ix].Any(x => bomb.GetModuleNames().Contains(x)) ? adjacentIndices[ix][1] : adjacentIndices[ix][0];
        }
        else
        {
            var indices = new int[][] { new int[] { 0, 3 }, new int[] { 1, 2 } }.First(x => !x.Contains(groups[0]) && !x.Contains(groups[1])).ToArray();
            groups[2] = bomb.GetPortPlateCount() % 2 == 0 ? indices[0] : indices[1];
        }
        groups[3] = Enumerable.Range(0, 4).First(x => groups[0] != x && groups[1] != x && groups[2] != x);
        Debug.LogFormat("[Tasque Managing #{0}] Groups (in reading order): {1}", moduleId, groups.Select(x => "ABCD"[x]).Join(", "));

        var tableSnowgrave = "DULULDRRLRLDRUDUUDDLRURLDULRLRUDLDURDLURDLUURDLRRULDRDLUURLRUDDL".Select(x => "URDL".IndexOf(x)).ToArray();
        var snIndices = new int[][] { new int[] { 3, 4 }, new int[] { 0, 2 }, new int[] { 1, 5 } };
        var base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var sn = bomb.GetSerialNumber();
        var startingConfiguration = new string[4];
        for (int i = 0; i < 3; i++)
        {
            var direction = tableSnowgrave[base36.IndexOf(sn[snIndices[i][0]]) % 8 * 8 + (base36.IndexOf(sn[snIndices[i][1]]) % 8)];
            while (!string.IsNullOrEmpty(startingConfiguration[direction]))
                direction = (direction + 3) % 4;
            startingConfiguration[direction] = "ABC"[i].ToString();
        }
        startingConfiguration[Array.IndexOf(startingConfiguration, null)] = "D";
        Debug.LogFormat("[Tasque Managing #{0}] Starting subtile configuration (clockwise order): {1}", moduleId, startingConfiguration.Join(", "));
        var startingDirection = 0;
        var evenModules = bomb.GetModuleNames().Count() % 2 == 0;
        var evenIndicators = bomb.GetIndicators().Count() % 2 == 0;
        if (!evenModules && evenIndicators)
            startingDirection = 0;
        else if (!evenModules && !evenIndicators)
            startingDirection = 1;
        else if (evenModules && !evenIndicators)
            startingDirection = 2;
        else
            startingDirection = 3;
        Debug.LogFormat("[Tasque Managing #{0}] The starting direction is {1}.", moduleId, new string[] { "up", "left", "right", "down" }[startingDirection]);
        subtiles[startingDirection] = startingConfiguration.Select(x => "ABCD".IndexOf(x[0])).ToArray();
        var directionXClock = bomb.GetBatteryCount() % 2 == 0;
        var directionYClock = bomb.GetBatteryHolderCount() % 2 == 1;
        Debug.LogFormat("[Tasque Managing #{0}] Direction X is {1}clockwise.", moduleId, directionXClock ? "" : "counter");
        Debug.LogFormat("[Tasque Managing #{0}] Direction Y is {1}clockwise.", moduleId, directionYClock ? "" : "counter");
        var currentDirection = startingDirection;
        for (int i = 0; i < 3; i++)
        {
            var prevConfig = subtiles[currentDirection].ToArray();
            if (directionYClock)
            {
                var s1 = prevConfig[0];
                var s2 = prevConfig[1];
                prevConfig[1] = s1;
                prevConfig[0] = s2;
                var s3 = prevConfig[0];
                var s4 = prevConfig[3];
                prevConfig[3] = s3;
                prevConfig[0] = s4;
                var s5 = prevConfig[2];
                var s6 = prevConfig[3];
                prevConfig[3] = s5;
                prevConfig[2] = s6;
            }
            else
            {
                var s1 = prevConfig[1];
                var s2 = prevConfig[2];
                prevConfig[2] = s1;
                prevConfig[1] = s2;
                var s3 = prevConfig[0];
                var s4 = prevConfig[2];
                prevConfig[2] = s3;
                prevConfig[0] = s4;
                var s5 = prevConfig[2];
                var s6 = prevConfig[3];
                prevConfig[3] = s5;
                prevConfig[2] = s6;
            }
            currentDirection = (currentDirection + (directionXClock ? 5 : 3)) % 4;
            subtiles[currentDirection] = prevConfig;
        }
        Debug.LogFormat("[Tasque Managing #{0}] Subtile configurations (clockwise order):", moduleId);
        for (int i = 0; i < 4; i++)
            Debug.LogFormat("[Tasque Managing #{0}] {1}", moduleId, subtiles[i].Select(x => "ABCD"[x]).Join(", "));
    }

    private void PressTile(KMSelectable tile)
    {
        tile.AddInteractionPunch(.1f);
        var ix = Array.IndexOf(tiles, tile);
        if (moduleSolved || !bombStarted || animating)
            return;
        if (!moduleActive)
        {
            if (ix != startingPosition)
                return;
            moduleActive = true;
            Debug.LogFormat("[Tasque Managing #{0}] Module activated, module activated!", moduleId);
            Debug.LogFormat("[Tasque Managing #{0}] Tiles to visit: {1}!", moduleId, goalTiles.Select(x => PositionName(x)).Join(", "));
            countUp = StartCoroutine(CountUp());
        }
        else
        {
            if (!movableTiles.Contains(ix))
                return;
            if (!maze[currentPosition].Contains(Array.IndexOf(movableTiles, ix).ToString()))
            {
                module.HandleStrike();
                Debug.LogFormat("[Tasque Managing #{0}] No, no! You ran into a wall!", moduleId);
            }
            else
            {
                tileRenders[currentPosition].material.color = tileColors[0];
                currentPosition = ix;
                movableTiles = adjacentTiles[ix].ToArray();
                if (!TwitchPlaysActive)
                    tileRenders[ix].material.color = tileColors[3];
                else
                    tileRenders[ix].material.color = tileColors[2];
                if (currentPosition == goalTiles[stage])
                {
                    Debug.LogFormat("[Tasque Managing #{0}] You've made it to {1}!", moduleId, PositionName(currentPosition));
                    leds[stage].material = litMat;
                    stage++;
                    if (stage == 3)
                    {
                        moduleSolved = true;
                        module.HandlePass();
                        audio.PlaySoundAtTransform("solve", transform);
                        Debug.LogFormat("[Tasque Managing #{0}] The module is solved, that it is!", moduleId);
                        tileRenders[currentPosition].material.color = tileColors[0];
                        if (countUp != null)
                        {
                            StopCoroutine(countUp);
                            countUp = null;
                        }
                        StartCoroutine(SolveAnimation());
                    }
                    else
                    {
                        if (countUp != null)
                        {
                            StopCoroutine(countUp);
                            countUp = null;
                        }
                        countUp = StartCoroutine(CountUp());
                    }
                }
            }
        }
    }

    private IEnumerator CountUp()
    {
        animating = true;
        var group = 0;
        for (int i = 0; i < 4; i++)
            if (groupIndices[i].Contains(goalTiles[stage]))
                group = i;
        var subtile = Array.IndexOf(groupIndices[group], goalTiles[stage]);
        audio.PlaySoundAtTransform("ABCD"[group].ToString(), transform);
        StartCoroutine(ShowLetter("ABCD"[group]));
        yield return new WaitForSeconds(.5f);
        audio.PlaySoundAtTransform("ABCD"[subtile].ToString(), transform);
        StartCoroutine(ShowLetter("ABCD"[subtile]));
        yield return new WaitForSeconds(.5f);
        animating = false;
        yield return new WaitForSeconds(waitTime);
        StartCoroutine(Strike());
    }

    private IEnumerator ShowLetter(char letter)
    {
        screenText.color = Color.white;
        screenText.text = letter.ToString();
        var elapsed = 0f;
        var duration = .49f;
        while (elapsed < duration)
        {
            screenText.color = Color.Lerp(Color.white, Color.clear, elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        screenText.color = Color.clear;
    }

    private IEnumerator Strike()
    {
        animating = true;
        if (countUp != null)
        {
            StopCoroutine(countUp);
            countUp = null;
        }
        Debug.LogFormat("[Tasque Managing #{0}] You ended up at {1}! No, no!", moduleId, PositionName(currentPosition));
        Debug.LogFormat("[Tasque Managing #{0}] Someone ought to whip you into shape! Back to the beginning you go!", moduleId);
        module.HandleStrike();
        audio.PlaySoundAtTransform("strike", transform);
        foreach (Renderer tile in tileRenders)
            tile.material.color = tileColors[4];
        yield return new WaitForSeconds(2f);
        stage = 0;
        foreach (Renderer tile in tileRenders)
            tile.material.color = tileColors[0];
        foreach (Renderer led in leds)
            led.material = blackMat;
        animating = false;
        moduleActive = false;
        Start();
    }

    private IEnumerator SolveAnimation()
    {
        var order = new int[] { 0, 2, 5, 9, 12, 14, 15, 13, 10, 6, 3, 1, 4, 8, 11, 7 };
        for (int i = 0; i < 16; i++)
        {
            tileRenders[order[i]].material.color = tileColors[5];
            yield return new WaitForSeconds(.1f);
        }
    }

    private static string PositionName(int ix)
    {
        var directions = new string[] { "up", "left", "right", "down" };
        var directionsButBlah = new string[] { "up", "right", "down", "left" };
        var part1 = 0;
        for (int i = 0; i < 4; i++)
            if (groupIndices[i].Contains(ix))
                part1 = i;
        var part2 = Array.IndexOf(groupIndices[part1], ix);
        return directions[part1] + "-" + directionsButBlah[part2];
    }

    // Twitch Plays
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "!{0} activate [Begins the module. On Twitch Plays, you have 30 seconds to move instead of 15.] !{0} <TL/TR/BL/BR> [Moves in that direction, can be chained with spaces, e.g. !{0} TL BL TR]";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string input)
    {
        var directions = new string[] { "TL", "TR", "BL", "BR" };
        input = input.Trim().ToUpperInvariant();
        if (input == "ACTIVATE" || input == "START" || input == "BEGIN" || input == "GO")
        {
            yield return null;
            tiles[startingPosition].OnInteract();
        }
        else if (input.Split(' ').All(x => directions.Contains(x)))
        {
            yield return null;
            foreach (string str in input.Split(' '))
            {
                var ix = Array.IndexOf(directions, str);
                if (movableTiles[ix] == -1)
                    yield break;
                else
                {
                    yield return new WaitForSeconds(.2f);
                    tiles[movableTiles[ix]].OnInteract();
                }
            }
        }
        else
            yield break;
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        while (!moduleSolved)
        {
            if (!moduleActive)
            {
                yield return null;
                tiles[startingPosition].OnInteract();
            }
            var q = new Queue<int>();
            var allMoves = new List<Movement>();
            q.Enqueue(currentPosition);
            while (q.Count > 0)
            {
                var next = q.Dequeue();
                if (next == goalTiles[stage])
                    goto readyToSubmit;
                var cell = maze[next];
                for (int i = 0; i < 4; i++)
                {
                    if (cell.Contains(i.ToString()) && !allMoves.Any(x => x.start == adjacentTiles[next][i]))
                    {
                        q.Enqueue(adjacentTiles[next][i]);
                        allMoves.Add(new Movement(next, adjacentTiles[next][i], i));
                    }
                }
            }
            throw new InvalidOperationException("There is a bug in maze generation.");
        readyToSubmit:
            while (animating)
                yield return true;
            if (allMoves.Count != 0) // Checks for position already being target
            {
                var lastMove = allMoves.First(x => x.end == goalTiles[stage]);
                var relevantMoves = new List<Movement> { lastMove };
                while (lastMove.start != currentPosition)
                {
                    lastMove = allMoves.First(x => x.end == lastMove.start);
                    relevantMoves.Add(lastMove);
                }
                for (int i = 0; i < relevantMoves.Count; i++)
                {
                    var thisMove = relevantMoves[relevantMoves.Count - 1 - i];
                    tiles[adjacentTiles[thisMove.start][thisMove.direction]].OnInteract();
                    yield return new WaitForSeconds(.1f);
                }
            }
        }
    }

    private class Movement
    {
        public int start { get; set; }
        public int end { get; set; }
        public int direction { get; set; }

        public Movement(int s, int e, int d)
        {
            start = s;
            end = e;
            direction = d;
        }
    }
}
