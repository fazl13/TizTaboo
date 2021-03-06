﻿using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace TizTaboo
{
    /// <summary>
    /// Хранит и обрабатывает список ссылок
    /// </summary>
    internal class Drive
    {
        public string fileId { get; set; } = "0";

        public DriveService Service { get; set; } = null;

        public Drive()
        {
            string[] Scopes = { DriveService.Scope.Drive };
            try
            {
                FileStream fs = new FileStream("client_id.json", FileMode.Open, FileAccess.Read);
                using (FileStream stream = new FileStream("client_id.json", FileMode.Open, FileAccess.Read))
                {
                    string credPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

                    credPath = Path.Combine(credPath, ".credentials/drive-tiztaboo.json");

                    UserCredential credential =
                        GoogleWebAuthorizationBroker.AuthorizeAsync(
                            GoogleClientSecrets.Load(stream).Secrets,
                            Scopes, GoogleClientSecrets.Load(fs).Secrets.ClientId,
                            CancellationToken.None,
                            new FileDataStore(credPath, true)
                        ).Result;

                    Service = new DriveService(
                        new BaseClientService.Initializer()
                        {
                            HttpClientInitializer = credential,
                            ApplicationName = "TizTaboo",
                        }
                    );
                    fs.Close();
                }
            }
            catch (Exception ex)
            {
                Log.Error("#201706021625: " + ex.Message);
            }
        }

        public string Find(string name)
        {
            string ret = "0";
            string pageToken = null;
            do
            {
                var request = Service.Files.List();
                request.Q = $"name='{name}'";
                request.Spaces = "drive";
                request.Fields = "nextPageToken, files(id, name)";
                request.PageToken = pageToken;
                var result = request.Execute();
                foreach (var file in result.Files)
                {
                    ret = file.Id;
                    break;
                }
                pageToken = result.NextPageToken;
            } while (pageToken != null);
            return ret;
        }

        /// <summary>
        ///  Проверяеn есть ли файл на
        /// </summary>
        /// <param name="idf"></param>
        /// <param name="service"></param>
        /// <returns></returns>
        public bool FileExists(string idf = null)
        {
            if (Service == null)
                return false;
            idf = idf ?? fileId;

            FilesResource.GetRequest getRequest = Service.Files.Get(idf);
            try
            {
                Google.Apis.Drive.v3.Data.File f = getRequest.Execute();
                if (f.Trashed != null && f.Trashed == true)
                {
                    return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Создает папку в облаке
        /// </summary>
        /// <param name="folderName"></param>
        /// <returns></returns>
        private string CreateFolder(string folderName)
        {
            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder"
            };
            var request = Service.Files.Create(fileMetadata);
            request.Fields = "id";
            var file = request.Execute();
            return file.Id;
        }

        /// <summary>
        /// Скачивает файл
        /// </summary>
        /// <param name="localFilePath">Куда</param>
        /// <returns></returns>
        public bool DownloadFile(string localFilePath)
        {
            if (Service == null)
                return false;
            FilesResource.GetRequest getRequest = Service.Files.Get(fileId);
            getRequest.Execute();
            using (FileStream fsDownload = new FileStream(localFilePath, FileMode.Create))
            {
                getRequest.Download(fsDownload);
            }
            return true;
        }

        /// <summary>
        /// Сохраняет в облаке файл
        /// </summary>
        /// <param name="fileId">Id файла</param>
        /// <param name="folderId">Id папки</param>
        /// <param name="localFilePath">Файл на диске который нужно залить</param>
        /// <returns></returns>
        public bool UploadFile(string localFilePath)
        {
            if (Service == null)
                return false;

            byte[] byteArray = File.ReadAllBytes(localFilePath);
            MemoryStream mStream = new MemoryStream(byteArray);

            // Файл для записи
            Google.Apis.Drive.v3.Data.File file = new Google.Apis.Drive.v3.Data.File()
            {
                Name = "TizTabooDataFile",
                MimeType = "application/octet-stream",
                Description = "Файл данных программы TizTaboo",
            };

            // Проверяем существует наш файл в облаке, если нет - создаем
            if (!FileExists())
            {
                file.Parents = new List<string> { CreateFolder("TizTabooData") };
                FilesResource.CreateMediaUpload createRequest = Service.Files.Create(file, mStream, file.MimeType);
                if (createRequest.Upload().Exception != null)
                {
                    Log.Error(createRequest.Upload().Exception.Message);
                    return false;
                }
                else
                {
                    fileId = createRequest.ResponseBody.Id;
                    return true;
                }
            }
            // Если есть, обновляем
            else
            {
                FilesResource.UpdateMediaUpload updateRequest = Service.Files.Update(file, fileId, mStream, file.MimeType);
                if (updateRequest.Upload().Exception != null)
                {
                    Log.Error(updateRequest.Upload().Exception.Message);
                    return false;
                }
                else
                    return true;
            }
        }
    }
}