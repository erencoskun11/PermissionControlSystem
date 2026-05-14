using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.AspNetCore.SignalR;

namespace PermissionControlSystem.SignalR
{
    public class NotificationHub : AbpHub
    {

       //(Broadcasting)Bu sınıfa bir şey yazmamamızın sebebi, tarayıcıdan (Client) sunucuya (Server) özel bir komut göndermiyor
        //olmamızdır. Sen sadece sunucuda bir olay olduğunda (yeni izin vb.) kullanıcılara anons yapmak istiyorsun.Mesajı Hub 
        //içinden değil, EventHandler içinden fırlattığın için bu sınıfın içi boş kalıyor.
    }
}
