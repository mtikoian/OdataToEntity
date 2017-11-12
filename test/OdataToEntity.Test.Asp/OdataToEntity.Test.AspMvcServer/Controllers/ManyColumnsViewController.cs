﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public class ManyColumnsViewController : OeBaseController
    {
        public ManyColumnsViewController(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
            : base(dataAdapter, edmModel)
        {
        }

        public async Task Get()
        {
            await base.Get(base.HttpContext, base.HttpContext.Response.Body, false, 10);
        }
    }
}