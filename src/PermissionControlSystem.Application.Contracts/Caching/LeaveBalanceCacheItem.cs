using System;
using System.Collections.Generic;
using System.Text;

namespace PermissionControlSystem.Caching
{
    // [CacheName("LeaveBalance")] // İsteğe bağlı olarak Redis'teki anahtar adını özelleştirebilirsin

    //string.Empty kullnamadık çünkü sayialr null olmaz 
    public class LeaveBalanceCacheItem
    {
        public int TotalEntitled { get; set; } // Toplamda 24 
        public int UsedLeave { get; set; }    // Bu yıl kullandığı (Örn: 4 gün)
        public int Balance { get; set; }       // Kalan (Örn: 10 gün)
        public LeaveBalanceCacheItem()
        {
            
        }


        public LeaveBalanceCacheItem(int totalEntitled,int usedLeave,int balance)
        {
            TotalEntitled = totalEntitled;
            UsedLeave = usedLeave;
            Balance = balance;
        }
    }
}
