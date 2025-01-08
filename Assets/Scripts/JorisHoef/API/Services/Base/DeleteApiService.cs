namespace JorisHoef.API.Services.Base
{
    public class DeleteApiService<TResponse> : ApiService<TResponse>
    {
        protected override HttpMethod HttpMethod => HttpMethod.DELETE;
    }
}