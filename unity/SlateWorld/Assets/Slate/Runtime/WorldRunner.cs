// SLATE — owns the running World and the clock. The sim is pure C# from the
// SlateSim package; this component just feeds it ticks. Speeds mirror the
// prototype: I / II / III = 1 / 6 / 24 sim-years per real second.
using System;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class WorldRunner : MonoBehaviour
    {
        public int seed = 7;

        [NonSerialized] public World World;

        public static readonly float[] SpeedsYearsPerSec = { 1f, 6f, 24f };
        public int speedIndex = 1;
        public bool paused;

        private double _monthAccumulator;
        private const int MaxTicksPerFrame = 60;

        public event Action<World> WorldRebuilt;   // fired after New World
        public event Action<ChronicleEvent> EventLogged;

        public string SpeedLabel => paused ? "paused" : new[] { "I", "II", "III" }[speedIndex];

        private void Awake()
        {
            BuildWorld(seed);
        }

        private void BuildWorld(int newSeed)
        {
            seed = newSeed;
            World = World.Create(newSeed);
            World.OnEvent = e => EventLogged?.Invoke(e);
            TerrainSampler.Bind(World);
        }

        // True while fast-forwarding, so effect layers can skip per-event flair.
        [NonSerialized] public bool BurstMode;

        // Advance the sim immediately (screenshot tooling, "skip ahead" debug).
        public void RunYearsImmediate(int years)
        {
            if (World == null) return;
            BurstMode = true;
            for (int i = 0; i < years * 12; i++) Simulation.Tick(World);
            BurstMode = false;
        }

        public void NewWorld()
        {
            // Seed choice is player input, not sim logic — wall-clock here is allowed.
            int newSeed = Environment.TickCount & 0xFFFF;
            BuildWorld(newSeed);
            WorldRebuilt?.Invoke(World);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) paused = !paused;
            if (Input.GetKeyDown(KeyCode.Alpha1)) { speedIndex = 0; paused = false; }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { speedIndex = 1; paused = false; }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { speedIndex = 2; paused = false; }
            if (Input.GetKeyDown(KeyCode.N)) NewWorld();

            if (paused || World == null) return;

            _monthAccumulator += Time.deltaTime * SpeedsYearsPerSec[speedIndex] * 12.0;
            int ticks = (int)_monthAccumulator;
            if (ticks <= 0) return;
            if (ticks > MaxTicksPerFrame) ticks = MaxTicksPerFrame; // don't spiral after a hitch
            _monthAccumulator -= ticks;
            for (int i = 0; i < ticks; i++) Simulation.Tick(World);
        }
    }
}
