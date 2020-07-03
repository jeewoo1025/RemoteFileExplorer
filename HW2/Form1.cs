using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using ClassLibrary1;

namespace HW2
{
    public partial class Form1 : Form
    {
        private string IP;
        private int port;
        private string path = "";

        private TcpListener m_listener;     // 서버 작동 리스너
        public NetworkStream m_Stream;      // 네트워크 스트림

        private byte[] sendBuffer = new byte[1024 * 4];
        private byte[] readBuffer = new byte[1024 * 4];

        private bool m_bClientOn = false;
        private Thread m_Thread;            // 쓰레드
        private Thread m_ThReader;          // Receive 쓰레드

        public Initialize m_initializeClass;
        public Select m_selectClass;
        public Expand m_expandClass;
        private DetailInfo m_detailClass;
        private Download m_downloadClass;

        TcpClient m_Client;                 // 클라이언트

        public Form1()
        {
            InitializeComponent();
        }

        private void btnServer_Click(object sender, EventArgs e)
        {
            if(path.Equals(""))
            {
                MessageBox.Show("경로를 선택해주세요.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                if(btnServer.Text.Equals("서버켜기"))
                {
                    if (txtPort.Text.Equals("") || txtIP.Text.Equals(""))
                    {
                        MessageBox.Show("IP 주소 혹은 port를 입력했는지 확인해주세요~");
                        return;
                    }

                    m_Thread = new Thread(new ThreadStart(ServerStart));
                    m_Thread.Start();

                    btnServer.Text = "서버끊기";
                    btnServer.ForeColor = Color.Red;
                }
                else
                {
                    ServerStop();

                    txtIP.Text = "";
                    txtPort.Text = "";

                    btnServer.Text = "서버켜기";
                    btnServer.ForeColor = Color.Black;
                }
            }
        }

        private void btnPath_Click(object sender, EventArgs e)
        {
            if(foldFileDlg.ShowDialog() == DialogResult.OK)
            {
                path = foldFileDlg.SelectedPath;
                txtPath.Text = path;
                txtLog.AppendText(path + "로 경로가 수정되었습니다.\r\n");
            }
        }

        public void Message(string msg)
        {
            if (this.m_Thread == null)
                return;

            this.Invoke(new MethodInvoker(delegate ()
            {
                txtLog.AppendText(msg + "\r\n");

                txtLog.Focus();
                txtLog.ScrollToCaret();         // 자동 스크롤
            }));
        }

        public void ServerStop()
        {
            if (!m_bClientOn)
            {
                Message("서버 종료");
                return;
            }

            m_bClientOn = false;

            m_listener.Stop();

            m_Stream.Close();

            m_Thread.Abort();
            m_ThReader.Abort();

            Message("서버 종료");
        }

        public void Send()
        {
            this.m_Stream.Write(this.sendBuffer, 0, this.sendBuffer.Length);
            this.m_Stream.Flush();

            Array.Clear(sendBuffer, 0, sendBuffer.Length);
        }

        public void sendInit()
        {
            if (!this.m_bClientOn)
                return;

            Initialize Init = new Initialize();
            Init.Type = (int)PacketType.초기화;
            Init.Data = path;                     // path 요청

            Packet.Serialize(Init).CopyTo(this.sendBuffer, 0);
            this.Send();
        }

        public void sendBeforeExpand(string obj)
        {
            if (!this.m_bClientOn)
                return;

            Expand expand = new Expand();
            expand.Type = (int)PacketType.확장;

            DirectoryInfo di = new DirectoryInfo(obj);
            expand.path = obj;
            expand.diArray = di.GetDirectories();

            Packet.Serialize(expand).CopyTo(this.sendBuffer, 0);
            this.Send();
        }

        public void sendBeforeSelect(string obj)
        {
            if (!this.m_bClientOn)
                return;

            Select select = new Select();
            select.Type = (int)PacketType.선택;

            DirectoryInfo di = new DirectoryInfo(obj);
            select.diArray = di.GetDirectories();
            select.fiArray = di.GetFiles();

            Packet.Serialize(select).CopyTo(this.sendBuffer, 0);
            this.Send();
        }

        public void sendDetailInfo(string obj)
        {
            if (!this.m_bClientOn)
                return;

            DetailInfo detail = new DetailInfo();
            detail.Type = (int)PacketType.상세정보;
            detail.fileInfo = new FileInfo(obj);

            Packet.Serialize(detail).CopyTo(this.sendBuffer, 0);
            this.Send();
        }

        public void sendDownload(string obj)
        {
            if (!this.m_bClientOn)
                return;

            Download download = new Download();
            download.Type = (int)PacketType.다운로드;
            download.fileName = obj;

            if (File.Exists(obj))
            {
                // 경로에 있는 파일 읽기
                using (FileStream fs = new FileStream(obj, FileMode.Open, FileAccess.Read))
                {
                    download.fileLength = fs.Length;
                    Packet.Serialize(download).CopyTo(this.sendBuffer, 0);
                    this.Send();

                    // 파일 길이만큼 전송
                    long offset = 0;
                    while (offset <= fs.Length)
                    {
                        fs.Read(sendBuffer, 0, sendBuffer.Length);
                        this.Send();

                        offset += sendBuffer.Length;
                    }
                }
            }
            else
            {
                Message("해당 파일이 존재하지 않습니다!!");
                return;
            }

            Message(obj + " 데이터 다운로드 완료...");
        }

        public void ServerStart()
        {
            m_Client = null;

            IP = txtIP.Text.Trim();
            port = Convert.ToInt32(txtPort.Text);

            try
            {
                m_listener = new TcpListener(IPAddress.Parse(IP), port);
                m_listener.Start();

                Message("클라이언트 접속 대기중...");
                m_Client = this.m_listener.AcceptTcpClient();

                if (m_Client.Connected)
                {
                    this.m_bClientOn = true;
                    Message("클라이언트 접속");

                    m_Stream = m_Client.GetStream();

                    m_ThReader = new Thread(new ThreadStart(RUN));
                    m_ThReader.Start();
                }
            }
            catch(Exception e)
            {
                Message(e.Message + "   시작 도중에 오류 발생");
                m_listener.Stop();
                m_Thread.Abort();

                if(m_Client.Connected)
                {
                    this.m_bClientOn = false;
                    this.m_Client.Close();
                    this.m_Stream.Close();
                }
                return;
            }
        }

        public void RUN()
        {
            try
            {
                int nRead = 0;
                while (this.m_bClientOn)
                {
                    Array.Clear(readBuffer, 0, readBuffer.Length);
                    nRead = this.m_Stream.Read(readBuffer, 0, 1024 * 4);

                    Packet packet = (Packet)Packet.Deserialize(this.readBuffer);

                    switch ((int)packet.Type)
                    {
                        case (int)PacketType.초기화:
                            {
                                Message("초기화 데이터 요청..");
                                sendInit();
                                break;
                            }
                        case (int)PacketType.선택:
                            {
                                Message("beforeSelect 요청..");

                                this.m_selectClass = (Select)Packet.Deserialize(this.readBuffer);
                                sendBeforeSelect(this.m_selectClass.path);
                                break;
                            }
                        case (int)PacketType.확장:
                            {
                                Message("beforeExpand 데이터 요청..");
                                this.m_expandClass = (Expand)Packet.Deserialize(this.readBuffer);

                                sendBeforeExpand(this.m_expandClass.path);
                                break;
                            }
                        case (int)PacketType.상세정보:
                            {
                                Message("상세정보 데이터 요청..");
                                this.m_detailClass = (DetailInfo)Packet.Deserialize(this.readBuffer);

                                sendDetailInfo(this.m_detailClass.path);
                                break;
                            }
                        case (int)PacketType.다운로드:
                            {
                                this.m_downloadClass = (Download)Packet.Deserialize(this.readBuffer);

                                sendDownload(this.m_downloadClass.fileName);
                                break;
                            }
                    }

                    if(!m_Client.Connected)
                    {
                        // 연결 끊어졌을 때
                        Message("client와 연결 끊어짐");

                        this.m_bClientOn = false;
                        this.m_Client.Close();
                        this.m_Stream.Close();
                        this.m_ThReader.Abort();
                    }
                }
            }
            catch
            {
                //Message(e.Message);
                return;
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if(this.m_listener != null)
                this.m_listener.Stop();

            if(this.m_Stream != null)
                this.m_Stream.Close();

            if(this.m_Thread != null)
                this.m_Thread.Abort();
        }
    }
}
