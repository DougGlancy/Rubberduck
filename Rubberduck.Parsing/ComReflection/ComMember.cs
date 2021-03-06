﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Rubberduck.Parsing.Symbols;
using ELEMDESC = System.Runtime.InteropServices.ComTypes.ELEMDESC;
using FUNCDESC = System.Runtime.InteropServices.ComTypes.FUNCDESC;
using INVOKEKIND = System.Runtime.InteropServices.ComTypes.INVOKEKIND;
using FUNCFLAGS = System.Runtime.InteropServices.ComTypes.FUNCFLAGS;

namespace Rubberduck.Parsing.ComReflection
{
    [DebuggerDisplay("{MemberDeclaration}")]
    public class ComMember : ComBase
    {
        public bool IsHidden { get; private set; }
        public bool IsRestricted { get; private set; }
        public bool IsEventHandler { get; private set; }
        public bool IsDefault { get; private set; }
        public bool IsEnumerator { get; private set; }
        public ComParameter ReturnType { get; private set; }
        public List<ComParameter> Parameters { get; set; }

        public ComMember(ITypeInfo info, FUNCDESC funcDesc) : base(info, funcDesc)
        {                      
            LoadParameters(funcDesc, info);
            var flags = (FUNCFLAGS)funcDesc.wFuncFlags;
            IsHidden = flags.HasFlag(FUNCFLAGS.FUNCFLAG_FHIDDEN);
            IsRestricted = flags.HasFlag(FUNCFLAGS.FUNCFLAG_FRESTRICTED);
            IsEventHandler = flags.HasFlag(FUNCFLAGS.FUNCFLAG_FSOURCE);
            IsDefault = flags.HasFlag(FUNCFLAGS.FUNCFLAG_FUIDEFAULT);
            IsEnumerator = flags.HasFlag(FUNCFLAGS.FUNCFLAG_FNONBROWSABLE) && Name.Equals("_NewEnum");
            SetDeclarationType(funcDesc, info);
        }

        private void SetDeclarationType(FUNCDESC funcDesc, ITypeInfo info)
        {
            if (IsEventHandler)
            {
                Type = DeclarationType.Event;
            }
            if (funcDesc.invkind.HasFlag(INVOKEKIND.INVOKE_PROPERTYGET))
            {
                Type = DeclarationType.PropertyGet;

            }
            else if (funcDesc.invkind.HasFlag(INVOKEKIND.INVOKE_PROPERTYPUT))
            {
                Type = DeclarationType.PropertyLet;
            }
            else if (funcDesc.invkind.HasFlag(INVOKEKIND.INVOKE_PROPERTYPUTREF))
            {
                Type = DeclarationType.PropertySet;
            }
            else if ((VarEnum)funcDesc.elemdescFunc.tdesc.vt == VarEnum.VT_VOID)
            {
                Type = DeclarationType.Procedure;
            }
            else
            {
                Type = DeclarationType.Function;
            }

            if (Type == DeclarationType.Function || Type == DeclarationType.PropertyGet)
            {
                ReturnType = new ComParameter(funcDesc.elemdescFunc, info, string.Empty);
            }
        }

        private void LoadParameters(FUNCDESC funcDesc, ITypeInfo info)
        {
            Parameters = new List<ComParameter>();
            var names = new string[255];
            int count;
            info.GetNames(Index, names, 255, out count);

            for (var index = 0; index < count - 1; index++)
            {
                var paramPtr = new IntPtr(funcDesc.lprgelemdescParam.ToInt64() + Marshal.SizeOf(typeof(ELEMDESC)) * index);
                var elemDesc = (ELEMDESC)Marshal.PtrToStructure(paramPtr, typeof(ELEMDESC));
                var param = new ComParameter(elemDesc, info, names[index + 1]);
                Parameters.Add(param);
            }
            if (Parameters.Any() && funcDesc.cParamsOpt == -1)
            {
                Parameters.Last().IsParamArray = true;
            }
        }

        // ReSharper disable once UnusedMember.Local
        private string MemberDeclaration
        {
            get
            {
                var type = string.Empty;
                switch (Type)
                {
                    case DeclarationType.Function:
                        type = "Function";
                        break;
                    case DeclarationType.Procedure:
                        type = "Sub";
                        break;
                    case DeclarationType.PropertyGet:
                        type = "Property Get";
                        break;
                    case DeclarationType.PropertyLet:
                        type = "Property Let";
                        break;
                    case DeclarationType.PropertySet:
                        type = "Property Set";
                        break;
                    case DeclarationType.Event:
                        type = "Event";
                        break;
                }
                return string.Format("{0} {1} {2}{3}{4}",
                    IsHidden || IsRestricted ? "Private" : "Public",
                    type,
                    Name,
                    ReturnType == null ? string.Empty : " As ",
                    ReturnType == null ? string.Empty : ReturnType.TypeName);
            }
        }
    }
}
