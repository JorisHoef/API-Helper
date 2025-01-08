using System;

namespace JorisHoef.API
{
    /// <summary>
    /// Keep in for now so we can implement login once the backend supports this
    /// </summary>
    public class ApiSession
    {
        public event Action SuccessfulLogin;
        
        public string AccessToken { get; private set; }
        
        private static ApiSession _current;
        
        public static ApiSession Instance => _current ??= new ApiSession();

        public void SetToken(string token)
        {
            this.AccessToken = token;
            this.SuccessfulLogin?.Invoke();
        }
    }
}