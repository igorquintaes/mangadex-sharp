﻿using System.Net.Http;

namespace MangaDexSharp;

/// <summary>
/// Represents all of the requests related to uploading chapters
/// </summary>
public interface IMangaDexUploadService
{
	/// <summary>
	/// Gets the current users active session
	/// </summary>
	/// <param name="token">The authentication token, if none is provided, it will fall back on the <see cref="ICredentialsService"/></param>
	/// <returns>The active upload session</returns>
	Task<MangaDexRoot<UploadSession>> Get(string? token = null);

	/// <summary>
	/// Starts an upload session for the current user
	/// </summary>
	/// <param name="manga">The ID of the manga</param>
	/// <param name="groups">The scanlation groups doing the upload</param>
	/// <param name="token">The authentication token, if none is provided, it will fall back on the <see cref="ICredentialsService"/></param>
	/// <returns>The new upload session</returns>
	Task<MangaDexRoot<UploadSession>> Begin(string manga, string[] groups, string? token = null);

	/// <summary>
	/// Starts editing a specific chapter
	/// </summary>
	/// <param name="chapterId">The ID of the chapter to edit</param>
	/// <param name="version">The version of the request</param>
	/// <param name="token">The authentication token, if none is provided, it will fall back on the <see cref="ICredentialsService"/></param>
	/// <returns>The current upload session</returns>
	Task<MangaDexRoot<UploadSession>> EditChapter(string chapterId, int version = 1, string? token = null);

	/// <summary>
	/// Uploads files to the given session
	/// </summary>
	/// <param name="sessionId">The ID of the session to upload to</param>
	/// <param name="token">The authentication token, if none is provided, it will fall back on the <see cref="ICredentialsService"/></param>
	/// <param name="files">All of the files to upload to the chapter (limit 10 per request)</param>
	/// <returns>The uploaded file results</returns>
	Task<UploadSessionFileList> Upload(string sessionId, string? token = null, params IFileUpload[] files);

	/// <summary>
	/// Deletes a specific file from the upload session
	/// </summary>
	/// <param name="sessionId">The ID of the session</param>
	/// <param name="fileId">The ID of the file to delete</param>
	/// <param name="token">The authentication token, if none is provided, it will fall back on the <see cref="ICredentialsService"/></param>
	/// <returns>The results of the request</returns>
	Task<MangaDexRoot> DeleteUpload(string sessionId, string fileId, string? token = null);

	/// <summary>
	/// Deletes multiple files from the upload session
	/// </summary>
	/// <param name="sessionId">The ID of the session</param>
	/// <param name="fileIds">The file IDs to delete</param>
	/// <param name="token">The authentication token, if none is provided, it will fall back on the <see cref="ICredentialsService"/></param>
	/// <returns>The results of the request</returns>
	Task<MangaDexRoot> DeleteUpload(string sessionId, string[] fileIds, string? token = null);

	/// <summary>
	/// Abandons and deletes the upload session
	/// </summary>
	/// <param name="sessionId">The ID of the session to abandon</param>
	/// <param name="token">The authentication token, if none is provided, it will fall back on the <see cref="ICredentialsService"/></param>
	/// <returns>The results of the request</returns>
	Task<MangaDexRoot> Abandon(string sessionId, string? token = null);

	/// <summary>
	/// Commits the upload session
	/// </summary>
	/// <param name="sessionId">The ID of the session to commit</param>
	/// <param name="data">The data of the session</param>
	/// <param name="token">The authentication token, if none is provided, it will fall back on the <see cref="ICredentialsService"/></param>
	/// <returns>The uploaded chapter</returns>
	Task<MangaDexRoot<Chapter>> Commit(string sessionId, UploadSessionCommit data, string? token = null);
}

internal class MangaDexUploadService : IMangaDexUploadService
{
	private readonly IApiService _api;
	private readonly ICredentialsService _creds;

	public string Root => $"{_creds.ApiUrl}/upload";

	public MangaDexUploadService(IApiService api, ICredentialsService creds)
	{
		_api = api;
		_creds = creds;
	}

	public async Task<MangaDexRoot<UploadSession>> Get(string? token = null)
	{
		var c = await Auth(token, _creds);
		return await _api.Get<MangaDexRoot<UploadSession>>(Root, c) ?? new() { Result = "error" };
	}

	public async Task<MangaDexRoot<UploadSession>> Begin(string manga, string[] groups, string? token = null)
	{
		var c = await Auth(token, _creds);
		var d = new UploadSessionCreate
		{
			Manga = manga,
			Groups = groups
		};
		return await _api.Post<MangaDexRoot<UploadSession>, UploadSessionCreate>($"{Root}/begin", d, c) ?? new() { Result = "error" };
	}

	public async Task<MangaDexRoot<UploadSession>> EditChapter(string chapterId, int version = 1, string? token = null)
	{
		var c = await Auth(token, _creds);
		var d = new EditChapterCreate
		{
			Version = version
		};
		return await _api.Post<MangaDexRoot<UploadSession>, EditChapterCreate>($"{Root}/begin/{chapterId}", d, c) ?? new() { Result = "error" };
	}

	public async Task<UploadSessionFileList> Upload(string sessionId, string? token = null, params IFileUpload[] files)
	{
		if (files.Length == 0)
			throw new ArgumentException("At least 1 file is required for an upload", nameof(files));

		if (files.Length > 10)
			throw new ArgumentException("There is a limit of 10 files at a tile for uploads", nameof(files));

		var c = await Auth(token, _creds);
		using var body = new MultipartFormDataContent();

		for(var i = 0; i < files.Length; i++)
		{
			var file = files[i];
			var data = await file.Content();
			body.Add(new ByteArrayContent(data), "file" + (i + 1), file.FileName);
		}

		return await _api.Create($"{Root}/{sessionId}", "POST")
			.With(c)
			.BodyContent(body)
			.Result<UploadSessionFileList>() ?? new() { Result = "error" };
	}

	public async Task<MangaDexRoot> DeleteUpload(string sessionId, string fileId, string? token = null)
	{
		var c = await Auth(token, _creds);
		return await _api.Delete<MangaDexRoot>($"{Root}/{sessionId}/{fileId}", c) ?? new() { Result = "error" };
	}

	public async Task<MangaDexRoot> DeleteUpload(string sessionId, string[] fileIds, string? token = null)
	{
		var c = await Auth(token, _creds);

		return await _api.Create($"{Root}/{sessionId}/batch", "DELETE")
			.With(c)
			.Body(fileIds)
			.Result<MangaDexRoot>() ?? new() {  Result = "error" };
	}

	public async Task<MangaDexRoot> Abandon(string sessionId, string? token = null)
	{
		var c = await Auth(token, _creds);
		return await _api.Delete<MangaDexRoot>($"{Root}/{sessionId}", c) ?? new() { Result = "error" };
	}

	public async Task<MangaDexRoot<Chapter>> Commit(string sessionId, UploadSessionCommit data, string? token = null)
	{
		var c = await Auth(token, _creds);
		return await _api.Post<MangaDexRoot<Chapter>, UploadSessionCommit>($"{Root}/{sessionId}/commit", data, c) ?? new() { Result = "error" };

	}
}
