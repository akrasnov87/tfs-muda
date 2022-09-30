using System;
using System.Collections.Generic;
using System.Text;

namespace TFS
{
    public static class GlobalSetting
    {
        /// <summary>
        /// адрес подключения к серверу
        /// </summary>
        public static string URL = "http://tfs2017.compulink.local:8080/tfs/DefaultCollection";

        /// <summary>
        /// Имя проетка
        /// </summary>
        public static string PROJECT_NAME = "IServ";

        /// <summary>
        /// Имя домена для авторизации
        /// </summary>
        public static string DOMAIN = "Compulink";
    }
}
