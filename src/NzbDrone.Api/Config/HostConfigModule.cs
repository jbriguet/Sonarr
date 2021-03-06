﻿using System.Linq;
using System.Reflection;
using FluentValidation;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Authentication;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Update;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;
using Omu.ValueInjecter;

namespace NzbDrone.Api.Config
{
    public class HostConfigModule : NzbDroneRestModule<HostConfigResource>
    {
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IUserService _userService;

        public HostConfigModule(IConfigFileProvider configFileProvider, IUserService userService)
            : base("/config/host")
        {
            _configFileProvider = configFileProvider;
            _userService = userService;

            GetResourceSingle = GetHostConfig;
            GetResourceById = GetHostConfig;
            UpdateResource = SaveHostConfig;

            SharedValidator.RuleFor(c => c.Branch).NotEmpty().WithMessage("Branch name is required, 'master' is the default");        
            SharedValidator.RuleFor(c => c.Port).ValidPort();

            SharedValidator.RuleFor(c => c.Username).NotEmpty().When(c => c.AuthenticationMethod != AuthenticationType.None);
            SharedValidator.RuleFor(c => c.Password).NotEmpty().When(c => c.AuthenticationMethod != AuthenticationType.None);

            SharedValidator.RuleFor(c => c.SslPort).ValidPort().When(c => c.EnableSsl);
            SharedValidator.RuleFor(c => c.SslCertHash).NotEmpty().When(c => c.EnableSsl && OsInfo.IsWindows);

            SharedValidator.RuleFor(c => c.UpdateScriptPath).IsValidPath().When(c => c.UpdateMechanism == UpdateMechanism.Script);

            SharedValidator.RuleFor(c => c.BindAddress)
                           .ValidIp4Address()
                           .NotListenAllIp4Address()
                           .When(c => c.BindAddress != "*");
        }

        private HostConfigResource GetHostConfig()
        {
            var resource = new HostConfigResource();
            resource.InjectFrom(_configFileProvider);
            resource.Id = 1;

            var user = _userService.FindUser();

            if (user != null)
            {
                resource.Username = user.Username;
                resource.Password = user.Password;
            }

            return resource;
        }

        private HostConfigResource GetHostConfig(int id)
        {
            return GetHostConfig();
        }

        private void SaveHostConfig(HostConfigResource resource)
        {
            var dictionary = resource.GetType()
                                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                     .ToDictionary(prop => prop.Name, prop => prop.GetValue(resource, null));

            _configFileProvider.SaveConfigDictionary(dictionary);

            if (resource.Username.IsNotNullOrWhiteSpace() && resource.Password.IsNotNullOrWhiteSpace())
            {
                _userService.Upsert(resource.Username, resource.Password);
            }
        }
    }
}
