using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using ClassLibrary1;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace HW2_Client
{
    public partial class Form1 : Form
    {
        private string IP;
        private int port;
        private string downloadPath = "";
        private string rootPath = "";

        private Thread m_Thread;                // Client 생성 쓰레드
        private Thread m_ThReader;              // Read할 때 쓰레드
        private NetworkStream m_Stream;      // 네트워크 스트림
        TcpClient m_Client;

        private byte[] sendBuffer = new byte[1024 * 4];
        private byte[] readBuffer = new byte[1024 * 4];

        private bool m_bConnect = false;     // 서버 접속 플래그

        private Initialize m_initializeClass;
        private Select m_selectClass;
        private Expand m_expandClass;
        private DetailInfo m_detailClass;
        private Download m_downloadClass;

        private TreeNode currentNode;
        private bool isOpen = false;

        public Form1()
        {
            InitializeComponent();
        }

        public void Send()
        {
            this.m_Stream.Write(this.sendBuffer, 0, this.sendBuffer.Length);
            this.m_Stream.Flush();

            Array.Clear(sendBuffer, 0, sendBuffer.Length);
        }

        public void sendInit()
        {
            if (!this.m_bConnect)
                return;

            Initialize Init = new Initialize();
            Init.Type = (int)PacketType.초기화;
            Init.Data = "";                     // path 요청

            Packet.Serialize(Init).CopyTo(this.sendBuffer, 0);
            this.Send();
        }

        public void sendBeforeSelect(string obj)
        {
            if (!this.m_bConnect || obj.Equals(""))
                return;

            Select select = new Select();
            select.Type = (int)PacketType.선택;
            select.path = obj;
            select.diArray = null;
            select.fiArray = null;

            Packet.Serialize(select).CopyTo(this.sendBuffer, 0);
            this.Send();
        }

        public void sendBeforeExpand(string obj)
        {
            if (!this.m_bConnect)
                return;

            Expand expand = new Expand();
            expand.Type = (int)PacketType.확장;
            expand.path = obj;
            expand.diArray = null;

            Packet.Serialize(expand).CopyTo(this.sendBuffer, 0);
            this.Send();
        }

        public void sendDetailInfo(string obj)
        {
            if (!this.m_bConnect)
                return;

            DetailInfo detail = new DetailInfo();
            detail.Type = (int)PacketType.상세정보;
            detail.path = obj;
            detail.fileInfo = null;

            Packet.Serialize(detail).CopyTo(this.sendBuffer, 0);
            this.Send();
        }

        public void sendDownload(string obj)
        {
            if (!this.m_bConnect)
                return;

            Download download = new Download();
            download.Type = (int)PacketType.다운로드;
            download.fileLength = 0;
            download.fileName = obj;

            Packet.Serialize(download).CopyTo(this.sendBuffer, 0);
            this.Send();
        }

        private void ClientStop()
        {
            if (!m_bConnect)
                return;

            this.m_bConnect = false;

            m_Client.Close();
            m_Stream.Close();

            m_Thread.Abort();
            m_ThReader.Abort();
        }

        private void ClientStart()
        {
            IP = txtIP.Text.Trim();
            port = Convert.ToInt32(txtPort.Text);

            try
            {
                this.m_Client = new TcpClient();
                this.m_Client.Connect(IP, port);
                this.m_bConnect = true;
                this.m_Stream = this.m_Client.GetStream();

                m_ThReader = new Thread(new ThreadStart(RUN));
                m_ThReader.Start();
            }
            catch(Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        private void RUN()
        {
            int nRead = 0;
            try
            {
                // 초기화 Packet을 Server에게 보냄
                sendInit();

                while (m_bConnect)
                {
                    nRead = 0;
                    Array.Clear(readBuffer, 0, readBuffer.Length);
                    nRead = this.m_Stream.Read(readBuffer, 0, 1024 * 4);
                    if (nRead == 0)
                        continue;

                    Packet packet = (Packet)Packet.Deserialize(this.readBuffer);

                    switch ((int)packet.Type)
                    {
                        case (int)PacketType.초기화:
                            {
                                // 해당 path의 모든 파일, dir들 얻어옴
                                this.m_initializeClass = (Initialize)Packet.Deserialize(this.readBuffer);
                                this.Invoke(new MethodInvoker(delegate ()
                                {
                                    rootPath = this.m_initializeClass.Data;

                                    TreeNode root = trvDir.Nodes.Add(rootPath);
                                    root.ImageIndex = 0;

                                    if (trvDir.SelectedNode == null)
                                        trvDir.SelectedNode = root;
                                    root.SelectedImageIndex = root.ImageIndex;
                                    root.Nodes.Add("");
                                }));
                                break;
                            }
                        case (int)PacketType.선택:
                            {
                                this.m_selectClass = (Select)Packet.Deserialize(this.readBuffer);
                                this.Invoke(new MethodInvoker(delegate ()
                                {
                                    BeforeSelect(this.m_selectClass.diArray, this.m_selectClass.fiArray);
                                }));
                                break;
                            }
                        case (int)PacketType.확장:
                            {
                                this.m_expandClass = (Expand)Packet.Deserialize(this.readBuffer);
                                this.Invoke(new MethodInvoker(delegate ()
                                {
                                    BeforeExpand(this.m_expandClass.diArray);

                                    if(isOpen)
                                    {
                                        OpenItem();
                                    }
                                }));
                                break;
                            }
                        case (int)PacketType.상세정보:
                            {
                                this.m_detailClass = (DetailInfo)Packet.Deserialize(this.readBuffer);
                                this.Invoke(new MethodInvoker(delegate ()
                                {
                                    make_Info(this.m_detailClass.fileInfo);
                                }));
                                break;
                            }
                        case (int)PacketType.다운로드:
                            {
                                // 서버에서 파일을 전송받아서 downloadPath에 저장한다.
                                this.m_downloadClass = (Download)Packet.Deserialize(this.readBuffer);

                                long offset = 0;
                                int size = 0;
                                string fileFullPath = (downloadPath + "\\" + this.m_downloadClass.fileName.Split('\\').Last());

                                long fileLength = this.m_downloadClass.fileLength;
                                using (FileStream ws = new FileStream(fileFullPath, FileMode.Create, FileAccess.Write))
                                {
                                    while (offset <= fileLength)
                                    {
                                        size = this.m_Stream.Read(readBuffer, 0, readBuffer.Length);
                                        ws.Write(readBuffer, 0, readBuffer.Length);

                                        offset += size;
                                    }
                                }
                                
                                break; 
                            }
                    }

                    if(!this.m_Client.Connected)
                    {
                        MessageBox.Show("서버 끊김");
                        ClientStop();
                    }
                }
            }
            catch
            {
                //MessageBox.Show(ex.Message);
                ClientStop();
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if(downloadPath.Equals(""))
            {
                MessageBox.Show("경로를 설정되어있지 않습니다.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                if (btnConnect.Text.Equals("서버연결"))
                {
                    if (txtIP.Text.Equals("") || txtPort.Text.Equals(""))
                    {
                        MessageBox.Show("IP 주소 혹은 port를 입력했는지 확인해주세요~");
                        return;
                    }

                    m_Thread = new Thread(new ThreadStart(ClientStart));
                    m_Thread.Start();

                    btnConnect.Text = "서버끊기";
                    btnConnect.ForeColor = Color.Red;
                }
                else
                {
                    ClientStop();

                    btnConnect.Text = "서버연결";
                    btnConnect.ForeColor = Color.Black;

                    txtIP.Text = "";
                    txtPort.Text = "";
                    lvwFiles.Items.Clear();
                    trvDir.Nodes.Clear();
                }
            }
        }

        private void btnPath_Click(object sender, EventArgs e)
        {
            if (foldFileDlg.ShowDialog() == DialogResult.OK)
            {
                downloadPath = foldFileDlg.SelectedPath;
                txtPath.Text = downloadPath;
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if(this.m_Client != null)
                this.m_Client.Close();

            if(this.m_Stream != null)
                this.m_Stream.Close();
        }

        public void setPlus(TreeNode node)
        {
            DirectoryInfo dir;
            DirectoryInfo[] di;

            try
            {
                dir = new DirectoryInfo(node.FullPath);
                di = dir.GetDirectories();
                if (di.Length > 0)
                    node.Nodes.Add("");
            }
            catch
            {
                //MessageBox.Show("setPlus : " + ex.Message);
                return;
            }
        }

        public void trvDir_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            currentNode = e.Node;

            sendBeforeExpand(e.Node.FullPath);
        }

        public void BeforeExpand(DirectoryInfo[] diArray)
        {
            TreeNode node;

            try
            {
                currentNode.Nodes.Clear();

                foreach (DirectoryInfo dirs in diArray)
                {
                    node = currentNode.Nodes.Add(dirs.Name);
                    setPlus(node);
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show("BeforeExpand : " + ex.Message);
            }
        }

        public void trvDir_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            sendBeforeSelect(e.Node.FullPath);
        }

        public int findIcon(string obj)
        {
            int index = 0;

            if (Regex.IsMatch(obj, ".txt", RegexOptions.IgnoreCase))
                index = 5;
            else if(Regex.IsMatch(obj, ".avi", RegexOptions.IgnoreCase) || Regex.IsMatch(obj, ".mp4", RegexOptions.IgnoreCase))
                index = 1;
            else if (Regex.IsMatch(obj, ".jpg", RegexOptions.IgnoreCase) || Regex.IsMatch(obj, ".png", RegexOptions.IgnoreCase))
                index = 2;
            else if (Regex.IsMatch(obj, ".mp3", RegexOptions.IgnoreCase) || Regex.IsMatch(obj, ".wav", RegexOptions.IgnoreCase))
                index = 3;
            else
                index = 4;

            return index;
        }

        public void BeforeSelect(DirectoryInfo[] diArray, FileInfo[] fiArray)
        {
            ListViewItem item;

            try
            {
                lvwFiles.Items.Clear();

                foreach (DirectoryInfo tdis in diArray)
                {
                    item = lvwFiles.Items.Add(tdis.Name);
                    item.SubItems.Add("");
                    item.SubItems.Add(tdis.LastWriteTime.ToString());
                    item.SubItems.Add(tdis.FullName);

                    item.ImageIndex = 0;
                    item.Tag = "D";
                }

                foreach (FileInfo fis in fiArray)
                {
                    item = lvwFiles.Items.Add(fis.Name);                
                    item.SubItems.Add(fis.Length.ToString());       // 크기
                    item.SubItems.Add(fis.LastWriteTime.ToString());  // 수정날짜
                    item.SubItems.Add(fis.FullName);

                    // 파일 타입에 따른 다른 아이콘 
                    item.ImageIndex = findIcon(fis.Extension);
                    item.Tag = "F";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("BeforeSelect" + ex.Message);
            }
        }

        public void mnuView_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;

            mnuDetail.Checked = false;
            mnuList.Checked = false;
            mnuSmall.Checked = false;
            mnuLarge.Checked = false;

            switch(item.Text)
            {
                case "자세히":
                    mnuDetail.Checked = true;
                    lvwFiles.View = View.Details;
                    break;
                case "간단히":
                    mnuList.Checked = true;
                    lvwFiles.View = View.List;
                    break;
                case "작은아이콘":
                    mnuSmall.Checked = true;
                    lvwFiles.View = View.SmallIcon;
                    break;
                case "큰아이콘":
                    mnuLarge.Checked = true;
                    lvwFiles.View = View.LargeIcon;
                    break;
            }
        }

        public void OpenItem()
        {
            TreeNode child = currentNode.FirstNode;
            ListViewItem item = lvwFiles.FocusedItem;

            while(child != null)
            {
                if (child.Text == item.Text)
                {
                    trvDir.SelectedNode = child;
                    trvDir.Focus();
                    break;
                }
                child = child.NextNode;
            }

            this.isOpen = false;
        }

        public void lvwFiles_DoubleClick(object sender, EventArgs e)
        {
            ListViewItem item = lvwFiles.FocusedItem;

            // 해당 폴더 찾기
            if(item.Tag.ToString().Equals("D"))
            {
                this.isOpen = true;
                trvDir.SelectedNode.Expand();
            }
            else if(item.Tag.ToString().Equals("F"))
            {
                string filePath = item.SubItems[3].Text;

                sendDetailInfo(filePath);
            }
        }

        private void mnuInfo_Click(object sender, EventArgs e)
        {
            string filePath = lvwFiles.FocusedItem.SubItems[3].Text;

            sendDetailInfo(filePath);
        }

        private void make_Info(FileInfo fi)
        {
            상세정보 newForm = new 상세정보(fi);
            newForm.Show();
        }

        private void mnuDownload_Click(object sender, EventArgs e)
        {
            ListViewItem item = lvwFiles.FocusedItem;

            // 해당 폴더 찾기
            if (item.Tag.ToString().Equals("D"))
            {
                MessageBox.Show("폴더는 다운로드를 지원하지 않습니다.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else if (item.Tag.ToString().Equals("F"))
            {
                sendDownload(item.SubItems[3].Text);
            }
        }

        private void lvwFiles_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            if (e.ColumnIndex == 3)
            {
                e.NewWidth = lvwFiles.Columns[e.ColumnIndex].Width;
                e.Cancel = true;
            }
        }
    }
}
