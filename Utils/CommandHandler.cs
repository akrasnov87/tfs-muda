using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFS.Utils
{
    public class CommandHandler
    {
        public string ProjectName { get; private set; }
        public MyVssConnection VssConnection { get; private set; }
        public string[] Args { get; private set; }

        public CommandHandler(string projectName, string[] args)
        {
            ProjectName = projectName;

            VssConnection = new MyVssConnection(GlobalSetting.DOMAIN, args[0], args[1]);

            Args = args;
        }

        /// <summary>
        /// Получение списка артефактов, у которых указана текущая итерация
        /// </summary>
        /// <param name="iterationPath">Итерация для выборки</param>
        /// <returns></returns>
        public async Task<IList<WorkItem>> GetCurrentIterationWorkItems(string iterationPath)
        {
            WorkItemTrackingHttpClient workItemTrackingHttpClient = VssConnection.GetConnection().GetClient<WorkItemTrackingHttpClient>();

            // https://docs.microsoft.com/en-us/azure/devops/boards/queries/wiql-syntax?view=azure-devops
            var wiql = new Wiql()
            {
                // NOTE: Even if other columns are specified, only the ID & URL will be available in the WorkItemReference
                Query = "Select [Id] " +
                        "From WorkItems " +
                        "Where [Work Item Type] <> 'Work' And [State] <> 'Closed' And [System.AssignedTo] = @me And [System.IterationPath] UNDER '" + iterationPath + "'"
            };

            // execute the query to get the list of work items in the results
            var result = await workItemTrackingHttpClient.QueryByWiqlAsync(wiql).ConfigureAwait(false);
            var ids = result.WorkItems.Select(item => item.Id).ToArray();

            // some error handling
            if (ids.Length == 0)
            {
                return Array.Empty<WorkItem>();
            }

            List<WorkItem> items = new List<WorkItem>();
            foreach(var id in ids)
            {
                items.Add(await workItemTrackingHttpClient.GetWorkItemAsync(id, null, result.AsOf, WorkItemExpand.Relations).ConfigureAwait(false));
            }

            // get work items for the ids found in query
            return items;
        }

        public async Task<WorkItem> UpdateWorkItem(int workId, string iterationPath)
        {
            WorkItemTrackingHttpClient workItemTrackingHttpClient = VssConnection.GetConnection().GetClient<WorkItemTrackingHttpClient>();
            JsonPatchDocument patchDocument = new JsonPatchDocument();
            patchDocument.Add(
                new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.IterationPath",
                    Value = iterationPath
                }
            );

            return await workItemTrackingHttpClient.UpdateWorkItemAsync(patchDocument, workId);
        }

        public async Task<WorkItem> CloseWorkItem(WorkItem item)
        {
            // обнуляем
            WorkItemTrackingHttpClient workItemTrackingHttpClient = VssConnection.GetConnection().GetClient<WorkItemTrackingHttpClient>();
            JsonPatchDocument patchDocument = new JsonPatchDocument();
            patchDocument.Add(new JsonPatchOperation()
            {
                Operation = Operation.Add,
                Path = "/fields/System.State",
                Value = "Resolved"
            });

            patchDocument.Add(new JsonPatchOperation()
            {
                Operation = Operation.Add,
                Path = "/fields/Microsoft.VSTS.Scheduling.RemainingWork",
                Value = "0"
            });

            await workItemTrackingHttpClient.UpdateWorkItemAsync(patchDocument, item.Id.Value);

            // закрываем
            patchDocument = new JsonPatchDocument();
            patchDocument.Add(new JsonPatchOperation()
            {
                Operation = Operation.Add,
                Path = "/fields/System.State",
                Value = "Closed"
            });

            return await workItemTrackingHttpClient.UpdateWorkItemAsync(patchDocument, item.Id.Value);
        }

        public async Task<WorkItem> CloseAnCreateWorkItem(WorkItem item, string iterationPath)
        {
            string[] systemFields = { "System.IterationId", "System.ExternalLinkCount", "System.HyperLinkCount", "System.AttachedFileCount", "System.NodeName",
    "System.RevisedDate", "System.ChangedDate", "System.Id", "System.AreaId", "System.AuthorizedAs", "System.State", "System.AuthorizedDate", "System.Watermark",
        "System.Rev", "System.ChangedBy", "System.Reason", "System.WorkItemType", "System.CreatedDate", "System.CreatedBy", "System.History", "System.RelatedLinkCount",
    "System.BoardColumn", "System.BoardColumnDone", "System.BoardLane", "System.CommentCount", "System.TeamProject"}; //system fields to skip

            string[] customFields = { "Microsoft.VSTS.Common.ActivatedDate", "Microsoft.VSTS.Common.ActivatedBy", "Microsoft.VSTS.Common.ResolvedDate",
        "Microsoft.VSTS.Common.ResolvedBy", "Microsoft.VSTS.Common.ResolvedReason", "Microsoft.VSTS.Common.ClosedDate", "Microsoft.VSTS.Common.ClosedBy",
    "Microsoft.VSTS.Common.StateChangeDate", "Microsoft.VSTS.Scheduling.CompletedWork", "Microsoft.VSTS.Scheduling.OriginalEstimate", "Microsoft.VSTS.Scheduling.RemainingWork"}; //unneeded fields to skip

            string ChildRefStr = "System.LinkTypes.Hierarchy-Forward";

            await CloseWorkItem(item);
            WorkItemTrackingHttpClient workItemTrackingHttpClient = VssConnection.GetConnection().GetClient<WorkItemTrackingHttpClient>();
            // создаём копию
            JsonPatchDocument patchDocument = new JsonPatchDocument();
            foreach (var key in item.Fields.Keys)
            {
                if (!systemFields.Contains(key) && !customFields.Contains(key))
                {
                    if(key == "System.IterationPath")
                    {
                        patchDocument.Add(new JsonPatchOperation()
                        {
                            Operation = Operation.Add,
                            Path = "/fields/" + key,
                            Value = iterationPath
                        });
                    } else
                    {
                        patchDocument.Add(new JsonPatchOperation()
                        {
                            Operation = Operation.Add,
                            Path = "/fields/" + key,
                            Value = item.Fields[key]
                        });
                    }
                }
            }

            patchDocument.Add(new JsonPatchOperation()
            {
                Operation = Operation.Add,
                Path = "/fields/Microsoft.VSTS.Scheduling.OriginalEstimate",
                Value = item.Fields["Microsoft.VSTS.Scheduling.RemainingWork"].ToString()
            });

            patchDocument.Add(new JsonPatchOperation()
            {
                Operation = Operation.Add,
                Path = "/fields/Microsoft.VSTS.Scheduling.RemainingWork",
                Value = item.Fields["Microsoft.VSTS.Scheduling.RemainingWork"].ToString()
            });

            foreach (var link in item.Relations)
            {
                if (link.Rel != ChildRefStr)
                {
                    patchDocument.Add(new JsonPatchOperation()
                    {
                        Operation = Operation.Add,
                        Path = "/relations/-",
                        Value = new
                        {
                            rel = link.Rel,
                            url = link.Url
                        }
                    });
                }
            }

            return await workItemTrackingHttpClient.CreateWorkItemAsync(patchDocument, GlobalSetting.PROJECT_NAME, "Task");
        }

        /// <summary>
        /// Получение информации о проекте
        /// </summary>
        /// <returns></returns>
        public async Task<TeamProjectReference> GetTeamResult()
        {
            ProjectHttpClient projectHttpClient = VssConnection.GetConnection().GetClient<ProjectHttpClient>();
            var result = await projectHttpClient.GetProjects(ProjectState.All).ConfigureAwait(false);
            IEnumerable<TeamProjectReference> teams = result.Where(t => t.Name == ProjectName);
            return teams.FirstOrDefault();
        }
    }
}
