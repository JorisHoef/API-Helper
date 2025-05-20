using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JorisHoef.APIHelper.Samples.Models;
using JorisHoef.APIHelper.Services;
using UnityEngine;

namespace JorisHoef.APIHelper.Samples.MonoBehaviours
{
    public class TestAPI : MonoBehaviour
    {
        private const string MOCK_API_URL = "https://jsonplaceholder.typicode.com/posts";
        private const int ID_TO_UPDATE = 1;

        //Add void entry-point methods since ContextMenu calls always have to return void, but we want our APICalls to return Task
        [ContextMenu("Test GET All")] private void TestGetAll() => _ = TestGetAllAsync();
        [ContextMenu("Test GET")] private void TestGet() => _ = TestGetAsync();
        [ContextMenu("Test POST")] private void TestPost() => _ = TestPostAsync();
        [ContextMenu("Test PUT")] private void TestPut() => _ = TestPutAsync();
        [ContextMenu("Test DELETE")] private void TestDelete() => _ = TestDeleteAsync();
        [ContextMenu("Test DELETE Will FAIL")] private void TestDeleteFail() => _ = TestDeleteFailAsync();

        private async Task TestGetAllAsync()
        {
            try
            {
                var response = await ApiServices.GetAsync<List<PostsData>>(MOCK_API_URL, false);
                if (response.IsSuccess)
                {
                    foreach (PostsData post in response.Data)
                    {
                        Debug.Log($"GET All succeeded: Title={post.Title}, UserId={post.UserId}");
                    }
                }
                else
                    Debug.LogError($"GET All failed: {response.Exception}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unhandled exception in TestGetAllAsync: {ex}");
            }
        }

        private async Task TestGetAsync()
        {
            var endPoint = $"{MOCK_API_URL}/{ID_TO_UPDATE}";
            try
            {
                var response = await ApiServices.GetAsync<PostsData>(endPoint, false);
                if (response.IsSuccess)
                    Debug.Log($"GET succeeded: Title={response.Data.Title}, UserId={response.Data.UserId}");
                else
                    Debug.LogError($"GET failed: {response.Exception}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unhandled exception in TestGetAsync: {ex}");
            }
        }

        private async Task TestPostAsync()
        {
            try
            {
                PostsData fakeData =
                        PostsData.CreateFakePostsData(0, "I am Title of Post Created!", "I am Content of Post");
                var response = await ApiServices.PostAsync<PostsData>(MOCK_API_URL, fakeData, false);
                if (response.IsSuccess)
                    Debug.Log($"POST succeeded: Title={response.Data.Title}, UserId={response.Data.UserId}");
                else
                    Debug.LogError($"POST failed: {response.Exception}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unhandled exception in TestPostAsync: {ex}");
            }
        }

        private async Task TestPutAsync()
        {
            var endPoint = $"{MOCK_API_URL}/{ID_TO_UPDATE}";
            try
            {
                PostsData fakeData =
                        PostsData.UpdateFakePostsData(ID_TO_UPDATE, 0, "I am Title of PostData Updated!",
                                                      "I am Contentof PostData");
                var response = await ApiServices.PutAsync<PostsData>(endPoint, fakeData, false);
                if (response.IsSuccess)
                    Debug.Log($"PUT succeeded: Title={response.Data.Title}, UserId={response.Data.UserId}");
                else
                    Debug.LogError($"PUT failed: {response.Exception}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unhandled exception in TestPutAsync: {ex}");
            }
        }

        private async Task TestDeleteAsync()
        {
            var endPoint = $"{MOCK_API_URL}/{ID_TO_UPDATE}";
            try
            {
                var response = await ApiServices.DeleteAsync<PostsData>(endPoint, false);
                if (response.IsSuccess)
                    Debug.Log($"DELETE succeeded: Title={response.Data.Title}, UserId={response.Data.UserId}");
                else
                    Debug.LogError($"DELETE failed: {response.Exception}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unhandled exception in TestDeleteAsync: {ex}");
            }
        }

        private async Task TestDeleteFailAsync()
        {
            var endPoint = $"{MOCK_API_URL}/{ID_TO_UPDATE}/123/123/123/123/123";
            try
            {
                var response = await ApiServices.DeleteAsync<PostsData>(endPoint, false);
                if (response.IsSuccess)
                    Debug.Log($"DELETE (fail test) succeeded unexpectedly: Title={response.Data.Title}, UserId={response.Data.UserId}");
                else
                    Debug.LogError($"DELETE (fail test) failed as expected: {response.Exception}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unhandled exception in TestDeleteFailAsync: {ex}");
            }
        }
    }
}