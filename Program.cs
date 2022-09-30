using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TFS.Utils;
using System.IO;

namespace TFS
{
    class Program
    {
        // из какой итерации брать информацию
        private string fromIterationPath;
        // в какую итерацию переводить
        private string toIterationPath;

        private CommandHandler commandHandler;

        public Program()
        {
            bool authorized = false;
            string login;
            string password;

            Console.WriteLine("Добро пожаловать в утилиту для перенова артефактов из одной итерации в другую. Для продолжения нажмите на любую клавишу...");
            Console.ReadLine();

            do
            {
                do
                {
                    Console.Clear();
                    Console.Write("Введите Ваш логин: ");
                } while (string.IsNullOrEmpty(login = Console.ReadLine()));

                do
                {
                    Console.Clear();
                    Console.Write("Введите Ваш пароль от логина {0}: ", login);
                } while (string.IsNullOrEmpty(password = Console.ReadLine()));

                // проверяем авторизацию
                commandHandler = new CommandHandler(GlobalSetting.PROJECT_NAME, new string[] { login, password });
                try
                {
                    commandHandler.GetTeamResult().GetAwaiter().GetResult();
                    authorized = true;
                }
                catch (Microsoft.VisualStudio.Services.Common.VssUnauthorizedException)
                {
                    Console.WriteLine("Ошибка авторизации.");
                }
            } while (!authorized);

            
            do
            {
                Console.Clear();
                Console.Write("Введите итерацию для поиска: ");
            } while (string.IsNullOrEmpty(fromIterationPath = Console.ReadLine()));

            
            do
            {
                Console.Clear();
                Console.Write("Введите итерацию, на которую меняем: ");
            } while (string.IsNullOrEmpty(toIterationPath = Console.ReadLine()));

            Console.Clear();
            Console.WriteLine("Ваш логин: {0}", login);

            Console.WriteLine("Итерация для поиска: {0}", fromIterationPath);
            Console.WriteLine("Итерация, на которую меняем: {0}", toIterationPath);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args">Первые два параметра это логин и пароль от TFS</param>
        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run();
        }

        private string normalAnswer(string answer)
        {
            if(answer.ToLower() == "yes")
            {
                return "да";
            }

            if (answer.ToLower() == "y")
            {
                return "да";
            }

            if (answer.ToLower() == "д")
            {
                return "да";
            }

            return answer;
        }

        public void Run()
        {
            Write("Поиск информации в TFS...");

            IList<WorkItem> workItems = commandHandler.GetCurrentIterationWorkItems(fromIterationPath).GetAwaiter().GetResult();

            if (workItems.Count > 0)
            {
                Write(string.Format("Найдено {0} записей:", workItems.Count));
                foreach (var workItem in workItems)
                {
                    Console.WriteLine("{0} {1} {2}", workItem.Fields["System.WorkItemType"], workItem.Id.Value, workItem.Fields["System.Title"]);
                }

                string answer;
                do
                {
                    Console.Write("Для замены итерации на \"{0}\" введите \"ДА\": ", toIterationPath);
                } while (string.IsNullOrEmpty(answer = Console.ReadLine()));

                if(normalAnswer(answer) == "да")
                {
                    UpdateIterationPath(workItems);
                    Write("Данные успешно обновлены, для завершения нажмите любую клавишу...");
                    Console.ReadLine();
                } else
                {
                    Write("Для завершения нажмите любую клавишу...");
                    Console.ReadLine();
                }
            } else
            {
                Write(string.Format("Записей, где IterationPath равно \"{0}\" не найдено", fromIterationPath));
            }
        }


        private void UpdateIterationPath(IList<WorkItem> workItems)
        {
            List<string> newItems = new List<string>();

            foreach (var workItem in workItems)
            {
                // для объектов task нужно проверить на наличие часов
                if(workItem.Fields["System.WorkItemType"].ToString() == "Task")
                {
                    string remainingWork = workItem.Fields["Microsoft.VSTS.Scheduling.RemainingWork"].ToString();
                    string originalEstimate = workItem.Fields["Microsoft.VSTS.Scheduling.OriginalEstimate"].ToString();

                    if (!string.IsNullOrEmpty(remainingWork.Trim())) 
                    {
                        float rw = float.Parse(remainingWork);
                        if (rw > 0 && remainingWork != originalEstimate)
                        {
                            Console.Clear();
                            Console.Write("У Task {1} есть остаток по часам {0}. Для закрытия текущего артефакта (по умолчанию) и создания нового введите \"Да\": ", remainingWork, workItem.Id.Value);
                            string answer = Console.ReadLine();
                            if(normalAnswer(answer) == "да")
                            {
                                // тут нужно изменить State и создать новую задачу
                                var t = commandHandler.CloseAnCreateWorkItem(workItem, toIterationPath).GetAwaiter().GetResult();
                                newItems.Add(t.Id.Value.ToString());
                                continue;
                            }
                        }
                    }

                    commandHandler.CloseWorkItem(workItem).GetAwaiter().GetResult();
                    continue;
                }

                commandHandler.UpdateWorkItem(workItem.Id.Value, toIterationPath).GetAwaiter().GetResult();
            }

            Write("Данные обработаны.");

            if (newItems.Count > 0) {
                Console.WriteLine("Созданы новые артефакты типа Task: " + string.Join(", ", newItems));
            }
        }

        private void Write(string message)
        {
            Console.Clear();
            Console.WriteLine(message);
        }
    }
}
