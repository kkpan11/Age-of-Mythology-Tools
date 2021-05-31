﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace AoMEngineLibrary.Graphics.Brg
{
    public class BrgBinaryReader : BinaryReader
    {
        public BrgBinaryReader(Stream stream)
            : base(stream)
        {
        }

        public BrgUserDataEntry ReadUserDataEntry(bool isParticle)
        {
            BrgUserDataEntry dataEntry;

            dataEntry.dataNameLength = this.ReadInt32();
            dataEntry.dataType = this.ReadInt32();
            switch (dataEntry.dataType)
            {
                case 1:
                    dataEntry.data = this.ReadInt32();
                    dataEntry.dataName = this.ReadString(dataEntry.dataNameLength + (int)dataEntry.data);
                    break;
                case 2:
                    dataEntry.data = this.ReadInt32();
                    dataEntry.dataName = this.ReadString(dataEntry.dataNameLength);
                    break;
                case 3:
                    dataEntry.data = this.ReadSingle();
                    dataEntry.dataName = this.ReadString(dataEntry.dataNameLength);
                    break;
                default:
                    dataEntry.data = this.ReadInt32();
                    dataEntry.dataName = this.ReadString(dataEntry.dataNameLength);
                    break;
            }

            return dataEntry;
        }

        public Vector3 ReadVector3D(bool isHalf)
        {
            Vector3 v = new Vector3();

            if (!isHalf)
            {
                v.X = this.ReadSingle();
                v.Y = this.ReadSingle();
                v.Z = this.ReadSingle();
            }
            else
            {
                v.X = this.ReadHalf();
                v.Y = this.ReadHalf();
                v.Z = this.ReadHalf();
            }

            return v;
        }

        public Vector2 ReadVector2D(bool isHalf = false)
        {
            Vector2 v = new Vector2();

            if (!isHalf)
            {
                v.X = this.ReadSingle();
                v.Y = this.ReadSingle();
            }
            else
            {
                v.X = this.ReadHalf();
                v.Y = this.ReadHalf();
            }

            return v;
        }

        public Vector4 ReadTexel()
        {
            var b = this.ReadByte() / 255.0f;
            var g = this.ReadByte() / 255.0f;
            var r = this.ReadByte() / 255.0f;
            var a = this.ReadByte() / 255.0f;
            return Vector4.Clamp(new Vector4(r, g, b, a),
                Vector4.Zero, Vector4.One);
        }

        public float ReadHalf()
        {
            byte[] f = new byte[4];
            byte[] h = this.ReadBytes(2);
            f[2] = h[0];
            f[3] = h[1];
            return BitConverter.ToSingle(f, 0);
        }
        public string ReadString(byte terminator = 0x0)
        {
            string filename = "";
            List<byte> fnBytes = new List<byte>();
            byte filenameByte = this.ReadByte();
            while (filenameByte != terminator)
            {
                filename += (char)filenameByte;
                fnBytes.Add(filenameByte);
                filenameByte = this.ReadByte();
            }
            return Encoding.UTF8.GetString(fnBytes.ToArray());
        }
        public string ReadString(int length)
        {
            return Encoding.UTF8.GetString(this.ReadBytes(length));
        }
    }
}
