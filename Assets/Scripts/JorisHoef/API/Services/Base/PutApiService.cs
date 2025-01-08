namespace JorisHoef.API.Services.Base
{
    public class PutApiService<TResponse> : ApiService<TResponse>
    {
        protected override HttpMethod HttpMethod => HttpMethod.PUT;
    }
}