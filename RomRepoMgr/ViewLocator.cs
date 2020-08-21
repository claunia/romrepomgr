// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using RomRepoMgr.ViewModels;

namespace RomRepoMgr
{
    public class ViewLocator : IDataTemplate
    {
        public bool SupportsRecycling => false;

        public IControl Build(object data)
        {
            string name = data.GetType().FullName?.Replace("ViewModel", "View");
            Type   type = name is null ? null : Type.GetType(name);

            return type is null ? new TextBlock
            {
                Text = "Not Found: " + name
            } : (Control)Activator.CreateInstance(type);
        }

        public bool Match(object data) => data is ViewModelBase;
    }
}