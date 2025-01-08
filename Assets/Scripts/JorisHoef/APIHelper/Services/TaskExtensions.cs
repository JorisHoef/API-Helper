using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace JorisHoef.APIHelper.Services
{
    public static class TaskExtensions
    {
        public static IEnumerator AsIEnumeratorWithCallback<T>(this Task<T> task, Action<T> onCompleted)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsCompletedSuccessfully)
            {
                onCompleted?.Invoke(task.Result);
            }
            else if (task.IsFaulted)
            {
                Debug.LogError($"Task failed with exception: {task.Exception}");
            }
        }
    }
}