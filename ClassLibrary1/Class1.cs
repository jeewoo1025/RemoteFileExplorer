using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace ClassLibrary1
{
    public enum PacketType
    {
        초기화=0,
        선택,
        확장,
        상세정보,
        다운로드
    }

    public enum PacketSendERROR
    {
        정상=0,
        에러
    }

    [Serializable]
    public class Packet
    {
        public int Length;
        public int Type;

        public Packet()
        {
            this.Length = 0;
            this.Type = 0;
        }

        public static byte[] Serialize(Object o)
        {
            // 객체 --> Stream
            MemoryStream ms = new MemoryStream(1024 * 4);
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(ms, o);

            return ms.ToArray();
        }

        public static Object Deserialize(byte[] bt)
        {
            // Stream --> 객체
            MemoryStream ms = new MemoryStream(1024 * 4);
            foreach (byte b in bt)
                ms.WriteByte(b);

            ms.Position = 0;
            BinaryFormatter bf = new BinaryFormatter();
            Object obj = bf.Deserialize(ms);
            ms.Close();

            return obj;
        }
    }

    [Serializable]
    public class Initialize : Packet
    {
        public string Data = "";
    }

    [Serializable]
    public class Select : Packet
    {
        public string path = "";
        public DirectoryInfo[] diArray;
        public FileInfo[] fiArray;

        public Select()
        {
            this.diArray = null;
            this.fiArray = null;
        }
    }

    [Serializable]
    public class Expand : Packet
    {
        public string path = "";
        public DirectoryInfo[] diArray;

        public Expand()
        {
            this.diArray = null;
        }
    }

    [Serializable]
    public class DetailInfo : Packet
    {
        public string path = "";
        public FileInfo fileInfo;

        public DetailInfo()
        {
            this.fileInfo = null;
        }
    }

    [Serializable]
    public class Download : Packet
    {
        public long fileLength;
        public string fileName = "";

        public Download()
        {
            this.fileLength = 0;
        }
    }
}
