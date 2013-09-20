﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DependencyInjection.FeatureTests.Adapters.Support;
using DependencyInjection.FeatureTests.Adapters.Support.GenericPlaceholders;
using MicroSliver;

namespace DependencyInjection.FeatureTests.Adapters {
    public class MicroSliverAdapter : FrameworkAdapterBase {
        private readonly IoC ioc = new IoC();

        #region DelegateCreator

        private class DelegateCreator : ICreator {
            private readonly Func<object> create;

            public DelegateCreator(Func<object> create) {
                this.create = create;
            }

            public object Create() {
                return this.create();
            }
        }

        #endregion
        
        public override Assembly FrameworkAssembly {
            get { return typeof(IoC).Assembly; }
        }

        public override void RegisterSingleton(Type serviceType, Type implementationType) {
            GenericHelper.RewriteAndInvoke(
                () => this.ioc.Map<P<X1>, C<X2, X1>>().ToSingletonScope(),
                serviceType, implementationType
            );
        }

        public override void RegisterTransient(Type serviceType, Type implementationType) {
            GenericHelper.RewriteAndInvoke(
                () => this.ioc.Map<P<X1>, C<X2, X1>>().ToInstanceScope(),
                serviceType, implementationType
            );
        }

        public override void RegisterInstance(Type serviceType, object instance) {
            GenericHelper.RewriteAndInvoke(
                () => this.ioc.Map<X1>(new DelegateCreator(() => instance)),
                serviceType
            );
        }

        public override object Resolve(Type serviceType) {
            return this.ioc.GetByType(serviceType);
        }

        public override bool CrashesOnRecursion {
            get { return true; }
        }
    }
}
