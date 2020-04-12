﻿namespace AoMEngineLibrary.Graphics.Brg
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;
    using System.Xml.Serialization;

    public class MtrlFile
    {
        private const string Zero = "0";
        private const string IndentString = "    ";

        public uint[] unk; //5   

        public Vector3 Diffuse { get; set; }
        public Vector3 Ambient { get; set; }
        public Vector3 Specular { get; set; }
        public Vector3 Emissive { get; set; }
        public float SpecularLevel { get; set; }
        public float Alpha { get; set; }

        public int Id { get; set; }

        public byte SelfIlluminating { get; set; }
        public byte ClampU { get; set; }
        public byte ClampV { get; set; }
        public byte LightSpecular { get; set; }
        public byte AffectsAmbient { get; set; }
        public byte AffectsDiffuse { get; set; }
        public byte AffectsSpecular { get; set; }
        public byte Updateable { get; set; }

        public int AlphaMode { get; set; } // Seems to be very often 10, wave has a 2 here, phoenix has 6
        public float AmbientIntensity { get; set; }
        public float DiffuseIntensity { get; set; }
        public float SpecularIntensity { get; set; }
        public float EmissiveIntensity { get; set; }
        public int ColorTransform { get; set; } // Val of 4 seems to be PC
        public int TextureTransform { get; set; }
        public uint TextureFactor { get; set; } // Has something to do with Cube Map
        public int MultiTextureMode { get; set; } // Has something to do with Cube Map
        public int TexGenMode0 { get; set; }
        public int TexGenMode1 { get; set; } // Has something to do with Cube Map
        public int TexCoordSet0 { get; set; }
        public int TexCoordSet1 { get; set; }
        public int TexCoordSet2 { get; set; }
        public int TexCoordSet3 { get; set; }
        public int TexCoordSet4 { get; set; }
        public int TexCoordSet5 { get; set; }
        public int TexCoordSet6 { get; set; }
        public int TexCoordSet7 { get; set; }
        
        public int TextureIndex { get; set; }
        public int SecondaryTextureIndex { get; set; }
        public int BumpMapIndex { get; set; }

        public int SpecularMapIndex { get; set; }
        public int GlossMapIndex { get; set; }
        public int EmissiveMapIndex { get; set; }

        public int[] Reserved { get; set; }

        public string Texture { get; set; }

        public MtrlFile()
        {
            this.unk = new uint[5];
            this.Alpha = 1f;

            this.Id = -1;

            this.SelfIlluminating = 0;
            this.ClampU = 0;
            this.ClampV = 0;
            this.LightSpecular = 1;
            this.AffectsAmbient = 1;
            this.AffectsDiffuse = 1;
            this.AffectsSpecular = 1;

            this.AlphaMode = 10;

            this.TextureIndex = -1;
            this.SecondaryTextureIndex = -1;
            this.BumpMapIndex = -1;

            this.SpecularMapIndex = -1;
            this.GlossMapIndex = -1;
            this.EmissiveMapIndex = -1;

            Reserved = new int[4];

            Texture = string.Empty;
        }
        public MtrlFile(BrgMaterial mat)
            : this()
        {
            this.Diffuse = mat.DiffuseColor;
            this.Ambient = mat.AmbientColor;
            this.Specular = mat.SpecularColor;
            this.Emissive = mat.EmissiveColor;
            this.SpecularLevel = mat.SpecularExponent;
            this.Alpha = mat.Opacity;

            if (!mat.Flags.HasFlag(BrgMatFlag.WrapUTx1))
            {
                this.ClampU = 1;
            }

            if (!mat.Flags.HasFlag(BrgMatFlag.WrapVTx1))
            {
                this.ClampV = 1;
            }

            if (mat.Flags.HasFlag(BrgMatFlag.AdditiveBlend))
            {
                this.AlphaMode = 6;
            }

            if (mat.Flags.HasFlag(BrgMatFlag.PixelXForm1))
            {
                this.ColorTransform = 4;
            }

            if (mat.Flags.HasFlag(BrgMatFlag.REFLECTIONTEXTURE))
            {
                this.TextureFactor = 1275068416;
                this.MultiTextureMode = 12;
                this.TexGenMode1 = 1;
            }

            this.Texture = mat.DiffuseMap;
        }

        public void Read(Stream stream)
        {
            using (BrgBinaryReader reader = new BrgBinaryReader(stream))
            {
                uint magic = reader.ReadUInt32();
                if (magic != 1280463949)
                {
                    throw new Exception("This is not a MTRL file!");
                }

                uint nameLength = reader.ReadUInt32();
                for (int i = 0; i < 5; ++i)
                {
                    this.unk[i] = reader.ReadUInt32();
                }

                this.Diffuse = reader.ReadVector3D(false);
                this.Ambient = reader.ReadVector3D(false);
                this.Specular = reader.ReadVector3D(false);
                this.Emissive = reader.ReadVector3D(false);
                this.SpecularLevel = reader.ReadSingle();
                this.Alpha = reader.ReadSingle();

                this.Id = reader.ReadInt32();

                this.SelfIlluminating = reader.ReadByte();
                this.ClampU = reader.ReadByte();
                this.ClampV = reader.ReadByte();
                this.LightSpecular = reader.ReadByte();
                this.AffectsAmbient = reader.ReadByte();
                this.AffectsDiffuse = reader.ReadByte();
                this.AffectsSpecular = reader.ReadByte();
                this.Updateable = reader.ReadByte();

                this.AlphaMode = reader.ReadInt32(); // Seems to be very often 10, wave has a 2 here, phoenix has 6
                this.AmbientIntensity = reader.ReadSingle();
                this.DiffuseIntensity = reader.ReadSingle();
                this.SpecularIntensity = reader.ReadSingle();
                this.EmissiveIntensity = reader.ReadSingle();
                this.ColorTransform = reader.ReadInt32(); // Val of 4 seems to be PC
                this.TextureTransform = reader.ReadInt32();
                this.TextureFactor = reader.ReadUInt32(); // Has something to do with Cube Map
                this.MultiTextureMode = reader.ReadInt32(); // Has something to do with Cube Map
                this.TexGenMode0 = reader.ReadInt32();
                this.TexGenMode1 = reader.ReadInt32(); // Has something to do with Cube Map
                this.TexCoordSet0 = reader.ReadInt32();
                this.TexCoordSet1 = reader.ReadInt32();
                this.TexCoordSet2 = reader.ReadInt32();
                this.TexCoordSet3 = reader.ReadInt32();
                this.TexCoordSet4 = reader.ReadInt32();
                this.TexCoordSet5 = reader.ReadInt32();
                this.TexCoordSet6 = reader.ReadInt32();
                this.TexCoordSet7 = reader.ReadInt32();

                this.TextureIndex = reader.ReadInt32();
                this.SecondaryTextureIndex = reader.ReadInt32();
                this.BumpMapIndex = reader.ReadInt32();

                this.SpecularMapIndex = reader.ReadInt32();
                this.GlossMapIndex = reader.ReadInt32();
                this.EmissiveMapIndex = reader.ReadInt32();

                for (int i = 0; i < 4; ++i)
                {
                    this.Reserved[i] = reader.ReadInt32();
                }

                if (nameLength > 0)
                {
                    this.Texture = reader.ReadString();
                }
            }
        }

        public void Write(Stream stream)
        {
            using (BrgBinaryWriter writer = new BrgBinaryWriter(stream, true))
            {
                writer.Write(1280463949); // MTRL
                UInt32 nameLength = (UInt32)Encoding.UTF8.GetByteCount(this.Texture);
                writer.Write(nameLength);
                for (int i = 0; i < 5; ++i)
                {
                    writer.Write(this.unk[i]);
                }

                writer.WriteVector3D(this.Diffuse, false);
                writer.WriteVector3D(this.Ambient, false);
                writer.WriteVector3D(this.Specular, false);
                writer.WriteVector3D(this.Emissive, false);
                writer.Write(this.SpecularLevel);
                writer.Write(this.Alpha);

                writer.Write(this.Id);

                writer.Write(this.SelfIlluminating);
                writer.Write(this.ClampU);
                writer.Write(this.ClampV);
                writer.Write(this.LightSpecular);
                writer.Write(this.AffectsAmbient);
                writer.Write(this.AffectsDiffuse);
                writer.Write(this.AffectsSpecular);
                writer.Write(this.Updateable);

                writer.Write(this.AlphaMode); // Seems to be very often 10, wave has a 2 here, phoenix has 6
                writer.Write(this.AmbientIntensity);
                writer.Write(this.DiffuseIntensity);
                writer.Write(this.SpecularIntensity);
                writer.Write(this.EmissiveIntensity);
                writer.Write(this.ColorTransform); // Val of 4 seems to be PC
                writer.Write(this.TextureTransform);
                writer.Write(this.TextureFactor); // Has something to do with Cube Map
                writer.Write(this.MultiTextureMode); // Has something to do with Cube Map
                writer.Write(this.TexGenMode0);
                writer.Write(this.TexGenMode1); // Has something to do with Cube Map
                writer.Write(this.TexCoordSet0);
                writer.Write(this.TexCoordSet1);
                writer.Write(this.TexCoordSet2);
                writer.Write(this.TexCoordSet3);
                writer.Write(this.TexCoordSet4);
                writer.Write(this.TexCoordSet5);
                writer.Write(this.TexCoordSet6);
                writer.Write(this.TexCoordSet7);

                writer.Write(this.TextureIndex);
                writer.Write(this.SecondaryTextureIndex);
                writer.Write(this.BumpMapIndex);

                writer.Write(this.SpecularMapIndex);
                writer.Write(this.GlossMapIndex);
                writer.Write(this.EmissiveMapIndex);

                for (int i = 0; i < 4; ++i)
                {
                    writer.Write(this.Reserved[i]);
                }

                if (nameLength > 0)
                {
                    writer.WriteString(this.Texture);
                }
            }
        }

        public void SerializeAsXml(Stream stream)
        {
            XDocument xdoc = new XDocument(new XElement("Material"));
            var elem = (XElement)xdoc.FirstNode;
            elem.Add(new XElement("diffuse", new XAttribute("R", Diffuse.X), new XAttribute("G", Diffuse.Y), new XAttribute("B", Diffuse.Z)));
            elem.Add(new XElement("ambient", new XAttribute("R", Ambient.X), new XAttribute("G", Ambient.Y), new XAttribute("B", Ambient.Z)));
            elem.Add(new XElement("specular", new XAttribute("R", Specular.X), new XAttribute("G", Specular.Y), new XAttribute("B", Specular.Z)));
            elem.Add(new XElement("emissive", new XAttribute("R", Emissive.X), new XAttribute("G", Emissive.Y), new XAttribute("B", Emissive.Z)));

            elem.Add(new XElement("specular_power", SpecularLevel));
            elem.Add(new XElement("alpha", Alpha));
            elem.Add(new XElement("id", Id));
            elem.Add(new XElement("self_illuminating", SelfIlluminating));
            elem.Add(new XElement("clamp_u", ClampU));
            elem.Add(new XElement("clamp_v", ClampV));

            elem.Add(new XElement("light_specular", LightSpecular));
            elem.Add(new XElement("affects_ambient", AffectsAmbient));
            elem.Add(new XElement("affects_diffuse", AffectsDiffuse));
            elem.Add(new XElement("affects_specular", AffectsSpecular));
            elem.Add(new XElement("updateable", Updateable));
            elem.Add(new XElement("alpha_mode", AlphaMode));

            elem.Add(new XElement("ambient_intensity", AmbientIntensity));
            elem.Add(new XElement("diffuse_intensity", DiffuseIntensity));
            elem.Add(new XElement("specular_intensity", SpecularIntensity));
            elem.Add(new XElement("emissive_intensity", EmissiveIntensity));

            elem.Add(new XElement("color_transform", ColorTransform));
            elem.Add(new XElement("texture_transform", TextureTransform));
            elem.Add(new XElement("texture_factor", TextureFactor));
            elem.Add(new XElement("multitexture_mode", MultiTextureMode));

            elem.Add(new XElement("texgen_mode_0", TexGenMode0));
            elem.Add(new XElement("texgen_mode_1", TexGenMode1));
            elem.Add(new XElement("texcoord_set_0", TexCoordSet0));
            elem.Add(new XElement("texcoord_set_1", TexCoordSet1));
            elem.Add(new XElement("texcoord_set_2", TexCoordSet2));
            elem.Add(new XElement("texcoord_set_3", TexCoordSet3));
            elem.Add(new XElement("texcoord_set_4", TexCoordSet4));
            elem.Add(new XElement("texcoord_set_5", TexCoordSet5));
            elem.Add(new XElement("texcoord_set_6", TexCoordSet6));
            elem.Add(new XElement("texcoord_set_7", TexCoordSet7));

            elem.Add(new XElement("texture", Texture));

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = IndentString,
                CloseOutput = false
            };
            using (XmlWriter writer = XmlWriter.Create(stream, settings))
            {
                xdoc.Save(writer);
            }
        }

        public static MtrlFile DeserializeAsXml(Stream stream)
        {
            MtrlFile file = new MtrlFile();
            XDocument xdoc = XDocument.Load(stream);
            XElement elem = xdoc.Root;

            foreach (var e in elem.Elements())
            {
                switch (e.Name.LocalName)
                {
                    case "diffuse":
                        file.Diffuse = new Vector3(float.Parse(e.Attribute("R")?.Value ?? Zero), float.Parse(e.Attribute("G")?.Value ?? Zero), float.Parse(e.Attribute("B")?.Value ?? Zero));
                        break;
                    case "ambient":
                        file.Ambient = new Vector3(float.Parse(e.Attribute("R")?.Value ?? Zero), float.Parse(e.Attribute("G")?.Value ?? Zero), float.Parse(e.Attribute("B")?.Value ?? Zero));
                        break;
                    case "specular":
                        file.Specular = new Vector3(float.Parse(e.Attribute("R")?.Value ?? Zero), float.Parse(e.Attribute("G")?.Value ?? Zero), float.Parse(e.Attribute("B")?.Value ?? Zero));
                        break;
                    case "emissive":
                        file.Emissive = new Vector3(float.Parse(e.Attribute("R")?.Value ?? Zero), float.Parse(e.Attribute("G")?.Value ?? Zero), float.Parse(e.Attribute("B")?.Value ?? Zero));
                        break;
                    case "specular_power":
                        file.SpecularLevel = float.Parse(e.Value);
                        break;
                    case "alpha":
                        file.Alpha = float.Parse(e.Value);
                        break;
                    case "id":
                        file.Id = int.Parse(e.Value);
                        break;
                    case "self_illuminating":
                        file.SelfIlluminating = byte.Parse(e.Value);
                        break;
                    case "clamp_u":
                        file.ClampU = byte.Parse(e.Value);
                        break;
                    case "clamp_v":
                        file.ClampV = byte.Parse(e.Value);
                        break;
                    case "light_specular":
                        file.LightSpecular = byte.Parse(e.Value);
                        break;
                    case "affects_ambient":
                        file.AffectsAmbient = byte.Parse(e.Value);
                        break;
                    case "affects_diffuse":
                        file.AffectsDiffuse = byte.Parse(e.Value);
                        break;
                    case "affects_specular":
                        file.AffectsSpecular = byte.Parse(e.Value);
                        break;
                    case "updateable":
                        file.Updateable = byte.Parse(e.Value);
                        break;
                    case "alpha_mode":
                        file.AlphaMode = int.Parse(e.Value);
                        break;
                    case "ambient_intensity":
                        file.AmbientIntensity = float.Parse(e.Value);
                        break;
                    case "diffuse_intensity":
                        file.DiffuseIntensity = float.Parse(e.Value);
                        break;
                    case "specular_intensity":
                        file.SpecularIntensity = float.Parse(e.Value);
                        break;
                    case "emissive_intensity":
                        file.EmissiveIntensity = float.Parse(e.Value);
                        break;
                    case "color_transform":
                        file.ColorTransform = int.Parse(e.Value);
                        break;
                    case "texture_transform":
                        file.TextureTransform = int.Parse(e.Value);
                        break;
                    case "texture_factor":
                        file.TextureFactor = uint.Parse(e.Value);
                        break;
                    case "multitexture_mode":
                        file.MultiTextureMode = int.Parse(e.Value);
                        break;
                    case "texgen_mode_0":
                        file.TexGenMode0 = int.Parse(e.Value);
                        break;
                    case "texgen_mode_1":
                        file.TexGenMode1 = int.Parse(e.Value);
                        break;
                    case "texcoord_set_0":
                        file.TexCoordSet0 = int.Parse(e.Value);
                        break;
                    case "texcoord_set_1":
                        file.TexCoordSet1 = int.Parse(e.Value);
                        break;
                    case "texcoord_set_2":
                        file.TexCoordSet2 = int.Parse(e.Value);
                        break;
                    case "texcoord_set_3":
                        file.TexCoordSet3 = int.Parse(e.Value);
                        break;
                    case "texcoord_set_4":
                        file.TexCoordSet4 = int.Parse(e.Value);
                        break;
                    case "texcoord_set_5":
                        file.TexCoordSet5 = int.Parse(e.Value);
                        break;
                    case "texcoord_set_6":
                        file.TexCoordSet6 = int.Parse(e.Value);
                        break;
                    case "texcoord_set_7":
                        file.TexCoordSet7 = int.Parse(e.Value);
                        break;
                    case "texture":
                        file.Texture = e.Value;
                        break;
                }
            }

            return file;
        }
    }
}
