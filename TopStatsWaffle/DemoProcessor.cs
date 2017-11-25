using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using TopStatsWaffle.Serialization;
using DemoInfo;

namespace TopStatsWaffle
{
    class MatchData
    {
        Dictionary<Type, List<object>> events = new Dictionary<Type, List<object>>();

        void addEvent(Type type, object ev)
        {
            //Create if doesnt exist
            if (!this.events.ContainsKey(type))
                this.events.Add(type, new List<object>());

            events[type].Add(ev);
        }

        public static MatchData fromDemoFile(string file)
        {
            MatchData md = new MatchData();

            //Create demo parser instance
            DemoParser dp = new DemoParser(File.OpenRead(file));

            dp.ParseHeader();

            // PLAYER EVENTS ===================================================
            dp.PlayerKilled += (object sender, PlayerKilledEventArgs e) =>{
                md.addEvent(typeof(PlayerKilledEventArgs), e);
            };

            // BOMB EVENTS =====================================================
            dp.BombPlanted += (object sender, BombEventArgs e) => {
                md.addEvent(typeof(BombEventArgs), e);
            };

            dp.BombDefused += (object sender, BombEventArgs e) => {
                md.addEvent(typeof(BombEventArgs), e);
            };

            // WEAPON EVENTS ===================================================
            dp.WeaponFired += (object sender, WeaponFiredEventArgs e) => {
                md.addEvent(typeof(WeaponFiredEventArgs), e);
            };

            // GRENADE EVENTS ==================================================
            dp.ExplosiveNadeExploded += (object sender, GrenadeEventArgs e) => {
                md.addEvent(typeof(GrenadeEventArgs), e);
            };

            dp.FireNadeStarted += (object sender, FireEventArgs e) => {
                md.addEvent(typeof(NadeEventArgs), e);
            };

            dp.SmokeNadeStarted += (object sender, SmokeEventArgs e) => {
                md.addEvent(typeof(SmokeEventArgs), e);
            };

            dp.FlashNadeExploded += (object sender, FlashEventArgs e) => {
                md.addEvent(typeof(FlashEventArgs), e);
            };


            dp.TickDone += (object sender, TickDoneEventArgs e) =>{
                md.addEvent(typeof(TickDoneEventArgs), e);
            };


            dp.ParseToEnd();

            

            dp.Dispose();

            for(int i = 0; i < md.events[typeof(WeaponFiredEventArgs)].Count; i++)
            {
                WeaponFiredEventArgs e = md.events[typeof(WeaponFiredEventArgs)][i] as WeaponFiredEventArgs;

                Console.WriteLine(e.Shooter.Name + " : " + e.Weapon.Class);
            }

            return md;
        }

        public void Save(string path)
        {
            //BinarySerialization.WriteToBinaryFile<MatchData>(path, this);
        }
    }
}
