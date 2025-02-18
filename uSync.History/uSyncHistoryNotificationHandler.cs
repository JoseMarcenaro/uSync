﻿using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Security;
using uSync.BackOffice;
using uSync.BackOffice.Configuration;
using uSync.BackOffice.Services;

namespace uSync.History
{
    internal class uSyncHistoryNotificationHandler 
        : INotificationHandler<uSyncImportCompletedNotification>,
        INotificationHandler<uSyncExportCompletedNotification>
    {
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly SyncFileService _syncFileService;
        private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
        private readonly ILogger<uSyncHistoryNotificationHandler> _logger;

        public uSyncHistoryNotificationHandler(
            SyncFileService syncFileService,
            IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
            IHostingEnvironment hostingEnvironment,
            ILogger<uSyncHistoryNotificationHandler> logger)
        {
            _syncFileService = syncFileService;
            _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
            _hostingEnvironment = hostingEnvironment;
            _logger = logger;
        }

        public void Handle(uSyncImportCompletedNotification notification)
        {
            var changeActions = notification.Actions
                .Where(x => x.Change > Core.ChangeType.NoChange && x.Change < Core.ChangeType.Hidden)
                .ToList();

            if (changeActions.Any())
            {
                SaveActions(changeActions, "Import", notification.Actions.Count());
            }
        }

        public void Handle(uSyncExportCompletedNotification notification)
        {
            var changeActions = notification.Actions
                .Where(x => x.Change > Core.ChangeType.NoChange && x.Change < Core.ChangeType.Hidden)
                .ToList();

            if (changeActions.Any())
            {
                SaveActions(changeActions, "Export", notification.Actions.Count());
            }
        }

        private void SaveActions(IEnumerable<uSyncAction> actions, string method, int total)
        {
            try
            {
                var historyInfo = new HistoryInfo
                {
                    Actions = actions,
                    Date = DateTime.Now,
                    Username = _backOfficeSecurityAccessor?.BackOfficeSecurity?.CurrentUser?.Username ?? "Background Process",
                    Method = method,
                    Total = total,
                    Changes = actions.CountChanges()
                };

                var historyJson = JsonConvert.SerializeObject(historyInfo, Formatting.Indented);

                var rootFolder = _syncFileService.GetAbsPath(_hostingEnvironment.LocalTempPath);
                var historyFile = Path.Combine(rootFolder, "uSync", "history", DateTime.Now.ToString("dd_MM_yyyy_HH_mm_ss") + ".json");

                _syncFileService.CreateFoldersForFile(historyFile);

                _syncFileService.SaveFile(historyFile, historyJson);
            }
            catch(Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save history.");
            }
        }
    }
}
