using System;
using System.Collections.Generic;
using System.Text;

namespace Domus
{
    public class User
    {
        public User(string username, string email, string name, string lastName, bool isAdmin, bool isActive, string createdAt, string lastLogin, string password = null, int userId = 0)
        {
            this.username = username;
            this.email = email;
            this.name = name;
            this.lastName = lastName;
            this.isAdmin = isAdmin;
            this.isActive = isActive;
            this.createdAt = createdAt;
            this.lastLogin = lastLogin;
            this.password = password;
            this.userId = userId;
        }


        public int userId { get; private set; }

        public string username { get; set; }

        public string email { get; set; }

        public string name { get; set; }

        public string lastName { get; set; }

        public bool isAdmin { get; set; }

        public bool isActive { get; set; }

        public string createdAt { get; set; }

        public string lastLogin { get; set; }

        public string password { get; set; }
    }
}
