using LestingCraft.Networking;
using NUnit.Framework;
using UnityEngine;

namespace LestingCraft.Tests.EditMode
{
    public class SpaceRoomPacketTests
    {
        [Test] public void RoomMissionSnapshotsParseWithoutConfusingTerrestrialPosition()
        {
            var welcome=JsonUtility.FromJson<NetPacket>(
                "{\"type\":\"welcome\",\"space\":{\"active\":true,\"from\":\"earth\",\"to\":\"moon\",\"seconds\":1800,\"phase\":\"cruise\",\"position\":[149990000000.25,40,2],\"pilot\":true,\"crewCount\":2,\"elapsed\":720},\"stationRequestId\":3}");
            Assert.IsNotNull(welcome.space);
            Assert.AreEqual("cruise",welcome.space.phase);
            Assert.AreEqual("earth",welcome.space.from);
            Assert.AreEqual("moon",welcome.space.to);
            Assert.AreEqual(1800,welcome.space.seconds);
            Assert.IsTrue(welcome.space.pilot);
            Assert.AreEqual(2,welcome.space.crewCount);
            Assert.AreEqual(149990000000.25,welcome.space.position[0],.1);
            Assert.AreEqual(3,welcome.stationRequestId);
            Assert.IsTrue(welcome.space.active);
            Assert.IsFalse(welcome.station!=null&&welcome.station.active);
            var station=JsonUtility.FromJson<NetPacket>(
                "{\"type\":\"welcome\",\"station\":{\"active\":true,\"dock\":{\"latched\":true,\"hatchOpen\":true,\"latchedAt\":123456789},\"local\":[-43.5,0,0],\"crewCount\":2}}");
            Assert.IsNotNull(station.station);
            Assert.IsTrue(station.station.dock.latched);
            Assert.IsTrue(station.station.dock.hatchOpen);
            Assert.AreEqual(-43.5f,station.station.local[0],.001f);
            Assert.IsTrue(station.station.active);
            Assert.IsFalse(station.space!=null&&station.space.active);
            var ordinary=JsonUtility.FromJson<NetPacket>("{\"type\":\"welcome\",\"seed\":2506}");
            Assert.IsFalse(ordinary.space!=null&&ordinary.space.active);
            Assert.IsFalse(ordinary.station!=null&&ordinary.station.active);
        }
    }
}
