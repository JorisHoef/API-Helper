using System.Collections.Generic;
using JorisHoef.APIHelper.Example.Models;
using JorisHoef.APIHelper.Models;
using JorisHoef.APIHelper.Services;
using UnityEngine;

namespace JorisHoef.APIHelper.Example
{
    public class TestAPI : MonoBehaviour
    {
        private const string MOCK_API_URL = "https://jsonplaceholder.typicode.com/posts";
        private const int ID_TO_UPDATE = 1;

        [ContextMenu("Test GET All")]
        private void TestGetAll()
        {
            string endPoint = $"{MOCK_API_URL}";

            StartCoroutine(ApiServices.GetAsync<List<PostsData>>(endPoint, false, null).AsIEnumeratorWithCallback(OnCompleted));
            return;

            void OnCompleted(ApiCallResult<List<PostsData>> responseBody)
            {
                if (responseBody.IsSuccess)
                {
                    foreach (var postsData in responseBody.Data)
                    {
                        Debug.Log($"Succeeded GET: {MOCK_API_URL} with Title: {postsData.Title} and UserId: {postsData.UserId}");
                    }

                }
                else
                {
                    Debug.Log($"Failed GET: {MOCK_API_URL} with Error {responseBody.Exception}");
                }
            }
        }

        [ContextMenu("Test GET")]
        private void TestGet()
        {
            string endPoint = $"{MOCK_API_URL}/{ID_TO_UPDATE}";

            StartCoroutine(ApiServices.GetAsync<PostsData>(endPoint, false, null).AsIEnumeratorWithCallback(OnCompleted));
            return;

            void OnCompleted(ApiCallResult<PostsData> responseBody)
            {
                Debug.Log(responseBody.IsSuccess
                                  ? $"Succeeded GET: {MOCK_API_URL} with Title: {responseBody.Data.Title} and UserId: {responseBody.Data.UserId}"
                                  : $"Failed GET: {MOCK_API_URL} with Error {responseBody.Exception}");
            }
        }

        [ContextMenu("Test POST")]
        private void TestPost()
        {
            string endPoint = $"{MOCK_API_URL}";

            var fakePostsData = PostsData.CreateFakePostsData(0, "I am Title of Post Created!", "I am Content of Post");

            StartCoroutine(ApiServices.PostAsync<PostsData>(endPoint, fakePostsData, false).AsIEnumeratorWithCallback(OnCompleted));
            return;

            void OnCompleted(ApiCallResult<PostsData> responseBody)
            {
                Debug.Log(responseBody.IsSuccess
                                  ? $"Succeeded POST: {MOCK_API_URL} with Title: {responseBody.Data.Title} and UserId: {responseBody.Data.UserId}"
                                  : $"Failed POST: {MOCK_API_URL} with Error {responseBody.Exception}");
            }
        }

        [ContextMenu("Test PUT")]
        private void TestPut()
        {
            string endPoint = $"{MOCK_API_URL}/{ID_TO_UPDATE}";

            var fakePostsData = PostsData.UpdateFakePostsData(ID_TO_UPDATE, 0, "I am Title of PostData Updated!", "I am Contentof PostData");

            StartCoroutine(ApiServices.PutAsync<PostsData>(endPoint, fakePostsData, false).AsIEnumeratorWithCallback(OnCompleted));
            return;

            void OnCompleted(ApiCallResult<PostsData> responseBody)
            {
                Debug.Log(responseBody.IsSuccess
                                  ? $"Succeeded PUT: {MOCK_API_URL} with Title: {responseBody.Data.Title} and UserId: {responseBody.Data.UserId}"
                                  : $"Failed PUT: {MOCK_API_URL} with Error {responseBody.Exception}");
            }
        }

        [ContextMenu("Test DELETE")]
        private void TestDelete()
        {
            string endPoint = $"{MOCK_API_URL}/{ID_TO_UPDATE}";

            StartCoroutine(ApiServices.DeleteAsync<PostsData>(endPoint, false, null).AsIEnumeratorWithCallback(OnCompleted));
            return;

            void OnCompleted(ApiCallResult<PostsData> responseBody)
            {
                Debug.Log(responseBody.IsSuccess
                                  ? $"Succeeded DELETE: {MOCK_API_URL} with Title: {responseBody.Data.Title} and UserId: {responseBody.Data.UserId}"
                                  : $"Failed DELETE: {MOCK_API_URL} with Error {responseBody.Exception}");
            }
        }

        [ContextMenu("Test DELETE Will FAIL")]
        private void TestDeleteFail()
        {
            string endPoint = $"{MOCK_API_URL}/{ID_TO_UPDATE}/123/123/123/123/123";

            StartCoroutine(ApiServices.DeleteAsync<PostsData>(endPoint, false, null).AsIEnumeratorWithCallback(OnCompleted));
            return;

            void OnCompleted(ApiCallResult<PostsData> responseBody)
            {
                Debug.Log(responseBody.IsSuccess
                                  ? $"Succeeded DELETE: {MOCK_API_URL} with Title: {responseBody.Data.Title} and UserId: {responseBody.Data.UserId}"
                                  : $"Failed DELETE: {MOCK_API_URL} with Error {responseBody.Exception}");
            }
        }
    }
}