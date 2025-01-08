using System.Collections.Generic;
using UnityEngine.Networking;

namespace JorisHoef.API.Services.MultipartForms
{
    /// <summary>
    /// Interface that allows classes to assign their own properties they want to POST in a multiformdatapost
    /// </summary>
    public interface IMultiFormPropertyAdder
    {
        public void AddPropertiesToForm(List<IMultipartFormSection> form);
    }
}