using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Data;


namespace CapturadorEkoMaiko
{
    class SQL
    {
        public SqlConnection _con { get; set; }
        public String _cad_Conexion { get; set; }
        public SqlDataReader _Resultado { get; set; }
        public SqlCommand _Comando { get; set; }

        /*actualizar*/
        public SQL(String cad_con = "Server=192.168.1.3;Database=basegpse;User ID=sa;Password=samtech2008;Trusted_Connection=False;")
        {
            _con = new SqlConnection();
            _cad_Conexion = String.Empty;
            _Comando = new SqlCommand();

            _con.ConnectionString = cad_con;
            _cad_Conexion = cad_con;
        }
        public void Conectar()
        {
            try
            {
                if (_con.State != ConnectionState.Open) {
                    _con.Open();
                }
                    
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
            }
        }
        public void ejecutar(String consulta, Boolean recordset)
        {
            try
            {
                _Comando.CommandText = consulta;
                _Comando.Connection = _con;
                if (recordset == true)
                {
                    _Resultado = _Comando.ExecuteReader();
                }
                else
                {
                    _Comando.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
            }
        }
        public void Desconectar()
        {
            _con.Close();
        }
        
    }


    public class AsynchronousSocketListener
    {
        public static string data = null;
        
        public static ManualResetEvent hiloPrincipal = new ManualResetEvent(false);
        public static IPEndPoint localIPEndPoint;
        public static IPEndPoint remoteIpEndPoint;
        public static EndPoint EndPointRemoto;
        public static EndPoint EndPointLocal;

        public static byte[] dataBuffer = new Byte[1024];

        public AsynchronousSocketListener()
        {
        }

        public static void StartListening()
        {

            /**
             * Configuracion del socket
             **/
            IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            

            localIPEndPoint = new IPEndPoint(ipAddress, 5050);
            remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

            EndPointRemoto = (EndPoint)remoteIpEndPoint;
            EndPointLocal = (EndPoint)localIPEndPoint;

            /**
             * Conexion del socket hacia la configuracion
             */
            try
            {
                Socket socketRecibidor = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socketRecibidor.Bind(EndPointLocal);


                while (true)
                {
                    hiloPrincipal.Reset();
                    Console.WriteLine("Esperando tramas.");
                    socketRecibidor.BeginReceiveFrom(dataBuffer, 0, dataBuffer.Length, SocketFlags.None, ref EndPointLocal, new AsyncCallback(TramaCallback), socketRecibidor);
                    hiloPrincipal.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\nAplicación finalizada, Aprete Enter.");
            Console.Read();

        }

        public static void TramaCallback(IAsyncResult ar)
        {
            try
            {
    
                /**
                 * Set del socket de entrada y endPoints
                 */

                Socket socketEntrada = (Socket)ar.AsyncState;
                int datosSocket = socketEntrada.EndReceiveFrom(ar, ref EndPointRemoto);
                socketEntrada.Connect(EndPointRemoto);
                var iplocal = (IPEndPoint)EndPointLocal;
                var ipremota = (IPEndPoint)EndPointRemoto;

                /*Set de variables de tramas*/
                String CONTENIDO = String.Empty;
                String[] partes;
                String ID = String.Empty;
                String GPS_ID = String.Empty;
                String LOG = String.Empty;
                String ACK = String.Empty;
                String Tipo = String.Empty;
                String Fecha = DateTime.Now.ToString();
                SQL conexion = new SQL();
                
                /*obtencion de los datos del socket*/
                
                if (datosSocket > 0) {

                    CONTENIDO = Encoding.ASCII.GetString(dataBuffer, 0, datosSocket);
                    partes = CONTENIDO.Split(';');



                    /** 
                     * TRAMAS EKOMAIKO
                     * PARTES[0] = >RUS08,EKO WISI0000000R11424154-7C,270115125721-3342442-070583591570001040190000000000
                     * PARTES[1] = ID=9337
                     * PARTES[2] = #LOG:323D
                     * PARTES[3] = *07<
                     */
                    
                    /** 
                     * TRAMAS TRACK
                     * PARTES[0] = >RPI260115100558-3342438-070581591470001090000100911190000000000EF46
                     * PARTES[1] = ID=9337
                     * PARTES[2] = #LOG:31EF
                     * PARTES[3] = *0B<
                     */
                    Tipo = partes[0].Substring(5,4);
                    Tipo = Tipo.Trim();

                    ID = partes[1].ToString();
                    LOG = partes[2].ToString();
                    GPS_ID = ID.Substring(3);


                    ACK = ">SAK;ID=" + GPS_ID + ";" + LOG + "<";
                    Console.WriteLine("Tipo: "+Tipo);
                    Console.WriteLine("GPS ID: " + GPS_ID);
                    Console.WriteLine("Fecha: " + Fecha);
                    if(CONTENIDO.ToString().Contains(">") == true || CONTENIDO.ToString().Contains("<") == true)
                    {
                        Send(socketEntrada, ACK + System.Environment.NewLine);
                        
                    }
                    else
                    {
                        Send(socketEntrada, ">NAK;ID=" + ID + ";" + LOG + "<" + System.Environment.NewLine);
                    }

                    
                    if (CONTENIDO.ToString().Contains("+ACK") == false)
                    {
                        conexion.Conectar();
                        if (Tipo.ToString().Contains("RUS")) {
                            conexion.ejecutar("INSERT INTO recipiente_ekomaiko (id_vehicle,datos,rx_date, tipo) VALUES ('" + GPS_ID + "','" + CONTENIDO + "','" + DateTime.Now.ToString() + "','" + Tipo + "')", false);
                        } else {
                            conexion.ejecutar("INSERT INTO recipiente_multiple_TRAX (id_vehicle,datos,rx_date) VALUES ('" + GPS_ID + "','" + CONTENIDO + "','" + DateTime.Now.ToString() + "')", false);
                        }
                        
                    }
                    
                    socketEntrada.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    socketEntrada.BeginReceiveFrom(dataBuffer, 0, dataBuffer.Length, SocketFlags.None, ref EndPointRemoto, new AsyncCallback(TramaCallback), socketEntrada);
                }
                
                

               
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message.ToString());
            }
            
        }

        private static void Send(Socket handler, String data)
        {
            byte[] byteData = Encoding.ASCII.GetBytes(data);
            handler.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None, EndPointRemoto, new AsyncCallback(SendCallback), handler);
            hiloPrincipal.Set();
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                int bytesSent = handler.EndSendTo(ar);
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public static int Main(String[] args)
        {
            StartListening();
            return 0;
        }
    }
}



 
