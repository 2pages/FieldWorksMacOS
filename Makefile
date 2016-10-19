# Copyright (c) 2007-2013 SIL International
# This software is licensed under the LGPL, version 2.1 or later
# (http://www.gnu.org/licenses/lgpl-2.1.html)
#
#	FieldWorks Makefile
#
#	MarkS - 2007-08-08

ICU_VERSION = 54
BUILD_ROOT = $(shell pwd)
include $(BUILD_ROOT)/Bld/_names.mak
BUILD_PRODUCT = FieldWorks
include $(BUILD_ROOT)/Bld/_init.mak.lnx
SHELL=/bin/bash

all: tlbs-copy teckit alltargets

# This is to facilitate easier use of CsProj files.
linktoOutputDebug:
	@-mkdir -p Output/
	@ln -sf $(OUT_DIR) Output/Debug
	@ln -sf $(OUT_DIR)/../XMI Output/XMI
	@ln -sf $(OUT_DIR)/../Common Output/Common


externaltargets: \
	Win32Base \
	COM-all \
	COM-install \
	Win32More \
	installable-COM-all \

externaltargets-test: \
	Win32Base-check \
	COM-check \
	Win32More-check \

nativetargets: \
	externaltargets \
	Generic-all \
	generic-Test \
	libAppCore \
	libFwKernel \
	Kernel-link \
	libCellar \
	libGraphiteTlb \
	libLanguage \
	libViews \
	libVwGraphics \
	views-link \
	views-Test \
	kernel-Test \
	language-Test \

alltargets: \
	nativetargets \
	FwResources \
	Utilities-BasicUtils \
	common-COMInterfaces \
	common-Utils \
	Utilities-MessageBoxExLib \
	Utilities-XMLUtils \
	common-FwUtils \
	common-Controls-Design \
	common-ScrUtilsInterfaces\
	common-ScriptureUtils \
	common-Controls-FwControls \
	Utilities-Reporting \
	FDO \
	XCore-Interfaces \
	common-UIAdapterInterfaces \
	common-SimpleRootSite \
	common-RootSite \
	common-Framework \
	common-Widgets \
	common-Filters \
	XCore \
	FwCoreDlgsGTK \
	FwCoreDlgGTKWidgets \
	FwCoreDlgs-FwCoreDlgControls \
	FwCoreDlgs-FwCoreDlgControlsGTK \
	FwCoreDlgs \
	DbAccess \
	LangInst \
	ComponentsMap \
	IcuDataFiles \
	install-strings \
	l10n-all \

test: \
	externaltargets-test \
	views-Test-check \
	kernel-Test-check \
	language-Test-check \
	generic-Test-check \

test-interactive: \
	FwCoreDlgs-SimpleTest \
	FwCoreDlgs-SimpleTest-check \

test-database: \
	DbAccessFirebird-check \

# Make copies of pre-generated files in Common.
tlbs-copy:
	@-mkdir -p $(COM_OUT_DIR)
	-cp -prf $(BUILD_ROOT)/Lib/linux/Common/* $(COM_OUT_DIR)/

tlbs-clean:
	$(RM) -rf $(COM_OUT_DIR)

# ICU on Linux looks for files (like unorm.icu uprops.icu) in
# $(BUILD_ROOT)/DistFiles/Icu54/icudt54l/icudt54l
# I think we need to move the *.icu into a sub dir
# currently copying incase anything trys to access *.icu in the first
# icudt50l directory.icudt
IcuDataFiles:
	(cd $(BUILD_ROOT)/DistFiles/Icu$(ICU_VERSION) && unzip -u ../Icu$(ICU_VERSION).zip)
	-(cd $(BUILD_ROOT)/DistFiles/Icu$(ICU_VERSION)/icudt$(ICU_VERSION)l && mkdir icudt$(ICU_VERSION)l)
	-(cd $(BUILD_ROOT)/DistFiles/Icu54/icudt$(ICU_VERSION)l && cp -p *.icu icudt$(ICU_VERSION)l)


# This build item isn't run on a normal build.
generate-strings:
	(cd $(SRC)/Language/ && $(BUILD_ROOT)/Bin/make-strings.sh Language.rc > $(BUILD_ROOT)/DistFiles/strings-en.txt)
	(cd $(SRC)/Generic/ && $(BUILD_ROOT)/Bin/make-strings.sh Generic.rc >> $(BUILD_ROOT)/DistFiles/strings-en.txt)
	(cd $(SRC)/Kernel/ && $(BUILD_ROOT)/Bin/make-strings.sh FwKernel.rc >> $(BUILD_ROOT)/DistFiles/strings-en.txt)
	(cd $(SRC)/views/ && $(BUILD_ROOT)/Bin/make-strings.sh Views.rc >> $(BUILD_ROOT)/DistFiles/strings-en.txt)
	(cd $(SRC)/AppCore/ && C_INCLUDE_PATH=./Res $(BUILD_ROOT)/Bin/make-strings.sh Res/AfApp.rc >> $(BUILD_ROOT)/DistFiles/strings-en.txt)

# now done in xbuild/msbuild
install-strings:
	cp -pf $(BUILD_ROOT)/DistFiles/strings-en.txt $(OUT_DIR)/strings-en.txt

# setup current sets up the mono registry necessary to run certain program
# This is now done in xbuild/msbuild.
setup:
	(cd $(BUILD_ROOT)/Build && xbuild /t:setRegistryValues)


clean: \
	COM-clean \
	COM-uninstall \
	COM-distclean \
	COM-autodegen \
	installable-COM-clean \
	Cellar-clean \
	Generic-clean \
	views-clean \
	AppCore-clean \
	Kernel-clean \
	Win32Base-clean \
	Win32More-clean \
	Graphite-GrEngine-clean \
	Language-clean \
	views-Test-clean \
	kernel-Test-clean \
	language-Test-clean \
	common-COMInterfaces-clean \
	common-SimpleRootSite-clean \
	common-RootSite-clean \
	common-Framework-clean \
	common-Widgets-clean \
	common-Utils-clean \
	common-FwUtils-clean \
	common-Filters-clean \
	common-UIAdapterInterfaces-clean \
	common-ScriptureUtils-clean \
	common-ScrUtilsInterfaces-clean \
	FwResources-clean \
	Utilities-BasicUtils-clean \
	Utilities-MessageBoxExLib-clean \
	Utilities-XMLUtils-clean \
	common-Controls-FwControls-clean \
	common-Controls-Design-clean \
	Utilities-Reporting-clean \
	FDO-clean \
	FwCoreDlgsGTK-clean \
	FwCoreDlgGTKWidgets-clean \
	FwCoreDlgs-FwCoreDlgControls-clean \
	FwCoreDlgs-FwCoreDlgControlsGTK-clean \
	FwCoreDlgs-clean \
	DbAccess-clean \
	XCore-Interfaces-clean \
	XCore-clean \
	LangInst-clean \
	ComponentsMap-clean \
	generic-Test-clean \
	tlbs-clean \
	teckit-clean \
	l10n-clean \
	manpage-clean \

# IDLImp is a C# app, so there is no reason to re-create that during our build.
# We should be able to just use the version in $(BUILD_ROOT)\Bin
tools: \
	Unit++-package \

tools-clean: \
	Unit++-clean \

idl: idl-do
# extracting the GUIDs is now done with a xbuild target, please run 'xbuild /t:generateLinuxIdlFiles'

idl-do:
	$(MAKE) -C$(SRC)/Common/COMInterfaces -f IDLMakefile all

idl-clean:
	$(MAKE) -C$(SRC)/Common/COMInterfaces -f IDLMakefile clean

fieldworks-flex.1.gz: DistFiles/Linux/fieldworks-flex.1.xml
	docbook2x-man DistFiles/Linux/fieldworks-flex.1.xml
	gzip fieldworks-flex.1
fieldworks-te.1.gz: DistFiles/Linux/fieldworks-te.1.xml
	docbook2x-man DistFiles/Linux/fieldworks-te.1.xml
	gzip fieldworks-te.1
unicodechareditor.1.gz: DistFiles/Linux/unicodechareditor.1.xml
	docbook2x-man DistFiles/Linux/unicodechareditor.1.xml
	gzip unicodechareditor.1
manpage-clean:
	rm -f fieldworks-*.1.gz unicodechareditor.1.gz

install-tree-fdo:
	# Create directories
	install -d $(DESTDIR)/usr/lib/fieldworks
	install -d $(DESTDIR)/usr/lib/fieldworks/icu-bin
	install -d $(DESTDIR)/usr/lib/pkgconfig
	install -d $(DESTDIR)/usr/share/fieldworks
	install -d $(DESTDIR)/var/lib/fieldworks
	# Install libraries and their support files
	install -m 644 $(OUT_DIR)/*.{dll*,so} $(DESTDIR)/usr/lib/fieldworks
	install -m 644 $(OUT_DIR)/{*.compmap,components.map} $(DESTDIR)/usr/lib/fieldworks
	install -m 644 Lib/src/icu/install$(ARCH)/lib/lib* $(DESTDIR)/usr/lib/fieldworks
	# Install executables and scripts
	install Lib/src/icu/install$(ARCH)/bin/* $(DESTDIR)/usr/lib/fieldworks/icu-bin
	install Lib/src/icu/source/bin/* $(DESTDIR)/usr/lib/fieldworks/icu-bin
	install Lib/linux/setup-user $(DESTDIR)/usr/share/fieldworks/
	# Install content and plug-ins
	# For reasons I don't understand we need strings-en.txt otherwise the tests fail when run from xbuild
	install -m 644 DistFiles/strings-en.txt $(DESTDIR)/usr/share/fieldworks
	install -m 644 DistFiles/*.{xml,map,tec,dtd} $(DESTDIR)/usr/share/fieldworks
	cp -pdr DistFiles/{Ethnologue,Icu54,Templates} $(DESTDIR)/usr/share/fieldworks
	# Remove unwanted items
	case $(ARCH) in i686) OTHERWIDTH=64;; x86_64) OTHERWIDTH=32;; esac; \
	rm -f $(DESTDIR)/usr/lib/fieldworks/lib{xample,patr}$$OTHERWIDTH.so
	rm -f $(DESTDIR)/usr/lib/fieldworks/lib{ecdriver,IcuConvEC,IcuRegexEC,IcuTranslitEC,PyScriptEncConverter}*.so
	rm -f $(DESTDIR)/usr/lib/fieldworks/{AIGuesserEC,CcEC,IcuEC,PerlExpressionEC,PyScriptEC,SilEncConverters40,ECInterfaces}.dll{,.config}
	rm -f $(DESTDIR)/usr/lib/fieldworks/libTECkit{,_Compiler}*.so
	rm -f $(DESTDIR)/usr/lib/fieldworks/{Geckofx-,Db4objects}*

install-tree: fieldworks-flex.1.gz fieldworks-te.1.gz unicodechareditor.1.gz install-tree-fdo
	# Create directories
	install -d $(DESTDIR)/usr/bin
	install -d $(DESTDIR)/usr/lib/fieldworks
	install -d $(DESTDIR)/usr/lib/fieldworks/Firefox
	install -d $(DESTDIR)/usr/share/fieldworks
	install -d $(DESTDIR)/usr/share/man/man1
	# Install libraries and their support files
	install -m 644 DistFiles/*.{dll*,so} $(DESTDIR)/usr/lib/fieldworks
	install -m 644 DistFiles/Linux/*.{dll*,so} $(DESTDIR)/usr/lib/fieldworks
	install -m 644 $(OUT_DIR)/*.{dll*,so} $(DESTDIR)/usr/lib/fieldworks
	install -m 644 $(OUT_DIR)/Firefox/*.* $(DESTDIR)/usr/lib/fieldworks/Firefox
	# Create temporary symlinks for shared Icu libs on Dictionary branch
	ln -sf $(DESTDIR)/usr/lib/fieldworks/libicuuc.so.54.1 $(DESTDIR)/usr/lib/fieldworks/libicuuc.so.50
	ln -sf $(DESTDIR)/usr/lib/fieldworks/libicui18n.so.54.1 $(DESTDIR)/usr/lib/fieldworks/libicui18n.so.50
	# Install read-only configuration files
	install -m 644 $(OUT_DIR)/remoting_tcp_server.config $(DESTDIR)/usr/lib/fieldworks
	# Install executables and scripts
	install $(OUT_DIR)/*.exe $(DESTDIR)/usr/lib/fieldworks
	install DistFiles/*.exe $(DESTDIR)/usr/lib/fieldworks
	install Bin/ReadKey.exe $(DESTDIR)/usr/lib/fieldworks
	install Bin/WriteKey.exe $(DESTDIR)/usr/lib/fieldworks
	install Lib/linux/fieldworks-{te,flex} $(DESTDIR)/usr/bin
	install Lib/linux/unicodechareditor $(DESTDIR)/usr/bin
	install Lib/linux/{cpol-action,run-app,extract-userws.xsl} $(DESTDIR)/usr/lib/fieldworks
	install Lib/linux/ShareFwProjects $(DESTDIR)/usr/lib/fieldworks
	install -m 644 environ{,-xulrunner} $(DESTDIR)/usr/lib/fieldworks
	install -m 644 Lib/linux/ShareFwProjects.desktop $(DESTDIR)/usr/share/fieldworks
	# Install content and plug-ins
	install -m 644 DistFiles/*.{pdf,txt,reg} $(DESTDIR)/usr/share/fieldworks
	cp -pdr DistFiles/{"Editorial Checks",EncodingConverters} $(DESTDIR)/usr/share/fieldworks
	cp -pdr DistFiles/{Helps,Fonts,Graphite,Keyboards,"Language Explorer",Parts} $(DESTDIR)/usr/share/fieldworks
	# Install man pages
	install -m 644 *.1.gz $(DESTDIR)/usr/share/man/man1
	# Handle the Converter files
	mv $(DESTDIR)/usr/lib/fieldworks/{Converter.exe,ConvertLib.dll,ConverterConsole.exe} $(DESTDIR)/usr/share/fieldworks
	# Remove unwanted items
	rm -f $(DESTDIR)/usr/lib/fieldworks/DevComponents.DotNetBar.dll
	case $(ARCH) in i686) OTHERWIDTH=64;; x86_64) OTHERWIDTH=32;; esac; \
	rm -f $(DESTDIR)/usr/lib/fieldworks/lib{xample,patr}$$OTHERWIDTH.so
	rm -f $(DESTDIR)/usr/lib/fieldworks/lib{ecdriver,IcuConvEC,IcuRegexEC,IcuTranslitEC,PyScriptEncConverter}*.so
	rm -f $(DESTDIR)/usr/lib/fieldworks/{AIGuesserEC,CcEC,IcuEC,PerlExpressionEC,PyScriptEC,SilEncConverters40,ECInterfaces}.dll{,.config}
	rm -f $(DESTDIR)/usr/lib/fieldworks/libTECkit{,_Compiler}*.so
	rm -Rf $(DESTDIR)/usr/lib/share/fieldworks/Icu54/tools
	rm -f $(DESTDIR)/usr/lib/share/fieldworks/Icu54/Keyboards
	# Remove localization data that came from "DistFiles/Language Explorer", which is handled separately by l10n-install
	rm -f $(DESTDIR)/usr/share/fieldworks/Language\ Explorer/Configuration/strings-*.xml
	# Except we still want strings-en.xml :-)
	install -m 644 DistFiles/Language\ Explorer/Configuration/strings-en.xml $(DESTDIR)/usr/share/fieldworks/Language\ Explorer/Configuration

install-menuentries:
	# Add to Applications menu
	install -d $(DESTDIR)/usr/share/pixmaps
	install -d $(DESTDIR)/usr/share/icons/hicolor/64x64/apps
	install -d $(DESTDIR)/usr/share/icons/hicolor/128x128/apps
	install -d $(DESTDIR)/usr/share/applications
	install -m 644 Src/LexText/LexTextExe/LT.png $(DESTDIR)/usr/share/pixmaps/fieldworks-flex.png
	install -m 644 Src/LexText/LexTextExe/LT64.png $(DESTDIR)/usr/share/icons/hicolor/64x64/apps/fieldworks-flex.png
	install -m 644 Src/LexText/LexTextExe/LT128.png $(DESTDIR)/usr/share/icons/hicolor/128x128/apps/fieldworks-flex.png
	install -m 644 Src/TeExe/Res/TE.png $(DESTDIR)/usr/share/pixmaps/fieldworks-te.png
	desktop-file-install --dir $(DESTDIR)/usr/share/applications Lib/linux/fieldworks-te.desktop
	desktop-file-install --dir $(DESTDIR)/usr/share/applications Lib/linux/fieldworks-flex.desktop
	desktop-file-install --dir $(DESTDIR)/usr/share/applications Lib/linux/unicodechareditor.desktop

install: install-tree install-menuentries l10n-install

install-package: install install-COM
	$(DESTDIR)/usr/lib/fieldworks/cpol-action pack

install-package-fdo: install-tree-fdo install-COM
	# Remove additional unwanted files
	rm -f $(DESTDIR)/usr/lib/fieldworks/FormattedEditor.dll*
	rm -f $(DESTDIR)/usr/lib/fieldworks/HelpSystem.dll*
	rm -f $(DESTDIR)/usr/lib/fieldworks/HtmlEditor.dll*
	rm -f $(DESTDIR)/usr/lib/fieldworks/LibChorus.dll*
	rm -f $(DESTDIR)/usr/lib/fieldworks/Interop.*
	rm -f $(DESTDIR)/usr/lib/fieldworks/NetLoc.*
	rm -f $(DESTDIR)/usr/lib/fieldworks/Palaso.Media.dll*
	rm -f $(DESTDIR)/usr/lib/fieldworks/PalasoUIWindowsForms*
	rm -f $(DESTDIR)/usr/lib/fieldworks/Paratext*
	rm -f $(DESTDIR)/usr/lib/fieldworks/SIL.Archiving*
	rm -f $(DESTDIR)/usr/lib/fieldworks/Utilities.*
	rm -f $(DESTDIR)/usr/lib/fieldworks/Vulcan.Uczniowie.HelpProvider*

uninstall: uninstall-menuentries
	rm -rf $(DESTDIR)/usr/bin/flex $(DESTDIR)/usr/lib/fieldworks $(DESTDIR)/usr/share/fieldworks

uninstall-menuentries:
	rm -f $(DESTDIR)/usr/share/pixmaps/fieldworks-te.png
	rm -f $(DESTDIR)/usr/share/pixmaps/fieldworks-flex.png
	rm -f $(DESTDIR)/usr/share/icons/hicolor/64x64/apps/fieldworks-flex.png
	rm -f $(DESTDIR)/usr/share/icons/hicolor/128x128/apps/fieldworks-flex.png
	rm -f $(DESTDIR)/usr/share/applications/fieldworks-{te,flex}.desktop

installable-COM-all:
	mkdir -p $(COM_DIR)/installer$(ARCH)
	-(cd $(COM_DIR)/installer$(ARCH) && [ ! -e Makefile ] && autoreconf -isf .. && \
		../configure --prefix=/usr/lib/fieldworks --libdir=/usr/lib/fieldworks)
	$(MAKE) -C$(COM_DIR)/installer$(ARCH) all

installable-COM-clean:
	$(RM) -r $(COM_DIR)/installer$(ARCH)

install-COM: installable-COM-all
	$(MAKE) -C$(COM_DIR)/installer$(ARCH) install

uninstall-COM:
	[ -e $(COM_DIR)/installer$(ARCH)/Makefile ] && \
	$(MAKE) -C$(COM_DIR)/installer$(ARCH) uninstall || true

##########


CTags-background-generation:
	echo Running ctags in the background...
	(nice -n20 /usr/bin/ctags -R --c++-types=+px --excmd=pattern --exclude=Makefile -f $(BUILD_ROOT)/tags.building $(BUILD_ROOT) $(WIN32BASE_DIR) $(WIN32MORE_DIR) $(COM_DIR) /usr/include && mv -f $(BUILD_ROOT)/tags.building $(BUILD_ROOT)/tags) &

Win32Base:
	$(MAKE) -C$(WIN32BASE_DIR) all
Win32Base-clean:
	$(MAKE) -C$(WIN32BASE_DIR) clean
Win32Base-check:
	$(MAKE) -C$(WIN32BASE_DIR) check

Win32More:
	$(MAKE) -C$(WIN32MORE_DIR) all
Win32More-clean:
	$(MAKE) -C$(WIN32MORE_DIR) clean
Win32More-check:
	$(MAKE) -C$(WIN32MORE_DIR) check

Generic-all: Generic-nodep
Generic-nodep:
	$(MAKE) -C$(SRC)/Generic all
Generic-clean:
	$(MAKE) -C$(SRC)/Generic clean
Generic-link:
	$(MAKE) -C$(SRC)/Generic link_check

DebugProcs-all: DebugProcs-nodep
DebugProcs-nodep:
	$(MAKE) -C$(SRC)/DebugProcs all
DebugProcs-clean:
	$(MAKE) -C$(SRC)/DebugProcs clean
DebugProcs-link:
	$(MAKE) -C$(SRC)/DebugProcs link_check

COM-all:
	-mkdir -p $(COM_BUILD)
	(cd $(COM_BUILD) && [ ! -e Makefile ] && autoreconf -isf .. && ../configure --prefix=`abs.py .`; true)
	REMOTE_WIN32_DEV_HOST=$(REMOTE_WIN32_DEV_HOST) $(MAKE) -C$(COM_BUILD) all
COM-install:
	$(MAKE) -C$(COM_BUILD) install
	@mkdir -p $(OUT_DIR)
	cp -pf $(COM_BUILD)/ManagedComBridge/libManagedComBridge.so $(OUT_DIR)/
COM-check:
	$(MAKE) -C$(COM_BUILD) check
COM-uninstall:
	[ -e $(COM_BUILD)/Makefile ] && \
	$(MAKE) -C$(COM_BUILD) uninstall || true
	rm -f $(OUT_DIR)/libManagedComBridge.so
COM-clean:
	[ -e $(COM_BUILD)/Makefile ] && \
	$(MAKE) -C$(COM_BUILD) clean || true
COM-distclean:
	[ -e $(COM_BUILD)/Makefile ] && \
	$(MAKE) -C$(COM_BUILD) distclean || true
COM-autodegen:
	(cd $(COM_DIR) && sh autodegen.sh)

Kernel-all: Kernel-nodep
Kernel-nodep: libFwKernel Kernel-link
libFwKernel:
	$(MAKE) -C$(SRC)/Kernel all
Kernel-componentsmap:
	$(MAKE) -C$(SRC)/Kernel ComponentsMap
Kernel-clean:
	$(MAKE) -C$(SRC)/Kernel clean
Kernel-link:
	$(MAKE) -C$(SRC)/Kernel link_check

views-all: views-nodep
views-nodep: libViews libVwGraphics views-link
libViews:
	$(MAKE) -C$(SRC)/views all
views-componentsmap:
	$(MAKE) -C$(SRC)/views ComponentsMap
views-clean:
	$(MAKE) -C$(SRC)/views clean
libVwGraphics:
	$(MAKE) -C$(SRC)/views libVwGraphics
views-link:
	$(MAKE) -C$(SRC)/views link_check

Cellar-all: Cellar-nodep
Cellar-nodep: libCellar
libCellar:
	$(MAKE) -C$(SRC)/Cellar all
Cellar-componentsmap:
	$(MAKE) -C$(SRC)/Cellar ComponentsMap
Cellar-clean:
	$(MAKE) -C$(SRC)/Cellar clean

AppCore-all: AppCore-nodep
AppCore-nodep: libAppCore
libAppCore:
	$(MAKE) -C$(SRC)/AppCore all
AppCore-clean:
	$(MAKE) -C$(SRC)/AppCore clean

Language-all: libFwKernel libViews Language-nodep
Language-nodep: libLanguage
libLanguage:
	$(MAKE) -C$(SRC)/Language all
Language-clean:
	$(MAKE) -C$(SRC)/Language clean
Language-link:
	$(MAKE) -C$(SRC)/Language link_check

Graphite-GrEngine-all: Graphite-GrEngine-nodep
Graphite-GrEngine-nodep: libGraphiteTlb
libGraphiteTlb:
	$(MAKE) -C$(SRC)/Graphite/GrEngine all
Graphite-GrEngine-clean:
	$(MAKE) -C$(SRC)/Graphite/GrEngine clean

unit++-all:
	-mkdir -p $(BUILD_ROOT)/Lib/src/unit++/build$(ARCH)
	([ ! -e $(BUILD_ROOT)/Lib/src/unit++/build$(ARCH)/Makefile ] && cd $(BUILD_ROOT)/Lib/src/unit++/build$(ARCH) && autoreconf -isf .. && ../configure ; true)
	$(MAKE) -C$(BUILD_ROOT)/Lib/src/unit++/build$(ARCH) all
unit++-clean:
	([ -e $(BUILD_ROOT)/Lib/src/unit++/build$(ARCH)/Makefile ] && $(MAKE) -C$(BUILD_ROOT)/Lib/src/unit++/build$(ARCH) clean ; true)
	-rm -rf $(BUILD_ROOT)/Lib/src/unit++/build$(ARCH)

views-Test:
	$(MAKE) -C$(SRC)/views/Test all
views-Test-clean:
	$(MAKE) -C$(SRC)/views/Test clean
views-Test-check:
	$(MAKE) -C$(SRC)/views/Test check

generic-Test-all: generic-Test
generic-Test:
	$(MAKE) -C$(SRC)/Generic/Test all
generic-Test-clean:
	$(MAKE) -C$(SRC)/Generic/Test clean
generic-Test-check:
	$(MAKE) -C$(SRC)/Generic/Test check

kernel-Test:
	$(MAKE) -C$(SRC)/Kernel/Test all
kernel-Test-clean:
	$(MAKE) -C$(SRC)/Kernel/Test clean
kernel-Test-check:
	$(MAKE) -C$(SRC)/Kernel/Test check

language-Test:
	$(MAKE) -C$(SRC)/Language/Test all
language-Test-clean:
	$(MAKE) -C$(SRC)/Language/Test clean
language-Test-check:
	$(MAKE) -C$(SRC)/Language/Test check

language-Test-check:

FwCoreDlgs-SimpleTest:
	$(MAKE) -C$(SRC)/FwCoreDlgs/SimpleTest all

FwCoreDlgs-SimpleTest-check:
	$(MAKE) -C$(SRC)/FwCoreDlgs/SimpleTest run


DbAccessFirebird-check:
	$(MAKE) -C$(SRC)/DbAccessFirebird check

# $(MAKE) Common items
common-COMInterfaces:
	(cd $(BUILD_ROOT)/Build && xbuild /t:COMInterfaces)
common-COMInterfaces-clean:
	(cd $(BUILD_ROOT)/Build && xbuild /t:COMInterfaces /property:action=clean)

common-Utils:
	$(MAKE) -C$(SRC)/Common/Utils all
common-Utils-clean:
	$(MAKE) -C$(SRC)/Common/Utils clean

common-FwUtils:
	$(MAKE) -C$(SRC)/Common/FwUtils all
common-FwUtils-clean:
	$(MAKE) -C$(SRC)/Common/FwUtils clean

common-SimpleRootSite:
	$(MAKE) -C$(SRC)/Common/SimpleRootSiteGtk all
common-SimpleRootSite-clean:
	$(MAKE)  -C$(SRC)/Common/SimpleRootSiteGtk clean

common-RootSite: common-SimpleRootSite
	$(MAKE) -C$(SRC)/Common/RootSite all
common-RootSite-clean:
	 $(MAKE) -C$(SRC)/Common/RootSite clean

common-Framework:
	$(MAKE) -C$(SRC)/Common/Framework all
common-Framework-clean:
	$(MAKE) -C$(SRC)/Common/Framework clean

common-Widgets:
	$(MAKE) -C$(SRC)/Common/Controls/Widgets all
common-Widgets-clean:
	$(MAKE) -C$(SRC)/Common/Controls/Widgets clean

common-Filters:
	$(MAKE) -C$(SRC)/Common/Filters all
common-Filters-clean:
	$(MAKE) -C$(SRC)/Common/Filters clean

common-UIAdapterInterfaces:
	$(MAKE) -C$(SRC)/Common/UIAdapterInterfaces all
common-UIAdapterInterfaces-clean:
	$(MAKE) -C$(SRC)/Common/UIAdapterInterfaces clean

common-ScriptureUtils:
	$(MAKE) -C$(SRC)/Common/ScriptureUtils all
common-ScriptureUtils-clean:
	$(MAKE) -C$(SRC)/Common/ScriptureUtils clean

common-ScrUtilsInterfaces:
	$(MAKE) -C$(SRC)/Common/ScrUtilsInterfaces all
common-ScrUtilsInterfaces-clean:
	$(MAKE) -C$(SRC)/Common/ScrUtilsInterfaces clean

common-Controls-FwControls:
	$(MAKE) -C$(SRC)/Common/Controls/FwControls all
common-Controls-FwControls-clean:
	$(MAKE) -C$(SRC)/Common/Controls/FwControls clean

common-Controls-Design:
	$(MAKE) -C$(SRC)/Common/Controls/Design all
common-Controls-Design-clean:
	$(MAKE) -C$(SRC)/Common/Controls/Design clean

Utilities-BasicUtils:
	$(MAKE) -C$(SRC)/Utilities/BasicUtils all
Utilities-BasicUtils-clean:
	$(MAKE) -C$(SRC)/Utilities/BasicUtils clean

Utilities-MessageBoxExLib:
	$(MAKE) -C$(SRC)/Utilities/MessageBoxExLib all
Utilities-MessageBoxExLib-clean:
	$(MAKE) -C$(SRC)/Utilities/MessageBoxExLib clean

Utilities-XMLUtils:
	$(MAKE) -C$(SRC)/Utilities/XMLUtils all
Utilities-XMLUtils-clean:
	$(MAKE) -C$(SRC)/Utilities/XMLUtils clean

Utilities-Reporting:
	$(MAKE) -C$(SRC)/Utilities/Reporting all
Utilities-Reporting-clean:
	$(MAKE) -C$(SRC)/Utilities/Reporting clean

FDO:
	$(MAKE) -C$(SRC)/FDO all
FDO-clean:
	$(MAKE) -C$(SRC)/FDO clean

FwResources:
	$(MAKE) -C$(SRC)/FwResources all
FwResources-clean:
	$(MAKE) -C$(SRC)/FwResources clean

FwCoreDlgsGTK:
	$(MAKE) -C$(SRC)/FwCoreDlgsGTK all
FwCoreDlgsGTK-clean:
	 $(MAKE) -C$(SRC)/FwCoreDlgsGTK clean

FwCoreDlgGTKWidgets:
	$(MAKE) -C$(SRC)/FwCoreDlgGTKWidgets all
FwCoreDlgGTKWidgets-clean:
	$(MAKE) -C$(SRC)/FwCoreDlgGTKWidgets clean

FwCoreDlgs-FwCoreDlgControls:
	$(MAKE) -C$(SRC)/FwCoreDlgs/FwCoreDlgControls all
FwCoreDlgs-FwCoreDlgControls-clean:
	$(MAKE) -C$(SRC)/FwCoreDlgs/FwCoreDlgControls clean

FwCoreDlgs-FwCoreDlgControlsGTK:
	$(MAKE) -C$(SRC)/FwCoreDlgs/FwCoreDlgControlsGTK all
FwCoreDlgs-FwCoreDlgControlsGTK-clean:
	$(MAKE) -C$(SRC)/FwCoreDlgs/FwCoreDlgControlsGTK clean

FwCoreDlgs:
	$(MAKE) -C$(SRC)/FwCoreDlgs all
FwCoreDlgs-clean:
	$(MAKE) -C$(SRC)/FwCoreDlgs clean

LangInst:
	$(MAKE) -C$(SRC)/LangInst all
LangInst-clean:
	$(MAKE) -C$(SRC)/LangInst clean

DbAccess-all: DbAccess-nodep
DbAccess: DbAccess-nodep
DbAccess-nodep:
	$(MAKE) -C$(SRC)/DbAccessFirebird all
DbAccess-clean:
	$(MAKE) -C$(SRC)/DbAccessFirebird clean

XCore-Interfaces:
	$(MAKE) -C$(SRC)/XCore/xCoreInterfaces all
XCore-Interfaces-clean:
	$(MAKE) -C$(SRC)/XCore/xCoreInterfaces clean

XCore:
	$(MAKE) -C$(SRC)/XCore all
XCore-clean:
	$(MAKE) -C$(SRC)/XCore clean

IDLImp-package:
	$(MAKE) -C$(BUILD_ROOT)/Bin/src/IDLImp package
IDLImp-clean:
	$(MAKE) -C$(BUILD_ROOT)/Bin/src/IDLImp clean

Unit++-package: unit++-all

Unit++-clean: unit++-clean

# We don't want CodeGen to be built by default. This messes things up if cmcg.exe gets checked
# in accidentally. Besides we currently don't use it, so I don't see a need for building it.
CodeGen:
	$(MAKE) -C$(BUILD_ROOT)/Bin/src/CodeGen
	# Put the cmcg executable where xbuild/msbuild is expecting it (cm.build.xml). Only do the cp if
	# the source file is newer than the target file, or if the target doesn't exist.
	if [ ! -e $(BUILD_ROOT)/Bin/cmcg.exe ] || \
		[ $(BUILD_ROOT)/Bin/src/CodeGen/CodeGen -nt $(BUILD_ROOT)/Bin/cmcg.exe ]; then \
		cp -p $(BUILD_ROOT)/Bin/src/CodeGen/CodeGen $(BUILD_ROOT)/Bin/cmcg.exe; fi

CodeGen-clean:
	$(MAKE) -C$(BUILD_ROOT)/Bin/src/CodeGen clean
	rm -f $(BUILD_ROOT)/Bin/cmcg.exe

teckit:
	if [ ! -e "$(OUT_DIR)/libTECkit_x86.so" ] || \
		[ "$(BUILD_ROOT)/DistFiles/Linux/libTECkit_x86.so" -nt "$(OUT_DIR)/libTECkit_x86.so" ]; then \
		mkdir -p $(OUT_DIR); \
		cp -p "$(BUILD_ROOT)/DistFiles/Linux/libTECkit_x86.so" "$(OUT_DIR)/libTECkit_x86.so"; fi
	if [ ! -e "$(OUT_DIR)/libTECkit_Compiler_x86.so" ] || \
		[ "$(BUILD_ROOT)/DistFiles/Linux/libTECkit_Compiler_x86.so" -nt "$(OUT_DIR)/libTECkit_Compiler_x86.so" ]; then \
		mkdir -p $(OUT_DIR); \
		cp -p "$(BUILD_ROOT)/DistFiles/Linux/libTECkit_Compiler_x86.so" "$(OUT_DIR)/libTECkit_Compiler_x86.so"; fi

teckit-clean:
	rm -f $(OUT_DIR)/libTECkit_x86.so $(OUT_DIR)/libTECkit_Compiler_x86.so

ComponentsMap: COM-all COM-install libFwKernel libLanguage libViews libCellar DbAccess ComponentsMap-nodep

ComponentsMap-nodep:
# the info gets now added by the xbuild/msbuild process.

ComponentsMap-clean:
	$(RM) $(OUT_DIR)/components.map

install-between-DistFiles:
	(cd $(OUT_DIR) && cp -pf ../../DistFiles/UIAdapters-simple.dll TeUIAdapters.dll)
	(cd DistFiles && cp -pf $(OUT_DIR)/FwResources.dll .)
	-(cd DistFiles && cp -pf $(OUT_DIR)/TeResources.dll .)
	(cd $(OUT_DIR) && ln -sf ../../DistFiles/Language\ Explorer/Configuration/ContextHelp.xml contextHelp.xml)

uninstall-between-DistFiles:
	rm $(OUT_DIR)/TeUIAdapters.dll
	rm DistFiles/FwResources.dll
	rm DistFiles/TeResources.dll

# // TODO-Linux: delete all C# makefiles and replace with xbuild/msbuild calls
BasicUtils-Nant-Build:
	(cd $(BUILD_ROOT)/Build && xbuild /t:BasicUtils)

Te-Nant-Build:
	(cd $(BUILD_ROOT)/Build && xbuild /t:allTe)

Te-Nant-Run:
	(cd $(BUILD_ROOT)/Build && xbuild /t:allTe /property:action=test)

Flex-Nant-Build:
	(cd $(BUILD_ROOT)/Build && xbuild /t:LexTextExe)

Flex-Nant-Run:
	(cd $(BUILD_ROOT)/Build && xbuild /t:LexTextExe /property:action=test)

TE: linktoOutputDebug tlbs-copy teckit externaltargets Te-Nant-Build install install-strings ComponentsMap-nodep Te-Nant-Run

Flex: linktoOutputDebug tlbs-copy externaltargets Flex-Nant-Build install-strings ComponentsMap-nodep Flex-Nant-Run

Fw:
	(cd $(BUILD_ROOT)/Build && xbuild /t:remakefw /property:action=test)

Fw-build:
	(cd $(BUILD_ROOT)/Build && xbuild /t:remakefw)

# Import certificates so mono applications can check ssl certificates, specifically when a build task
# downloads dependency dlls. Output md5sum of certificates imported for the record.
InstallCerts:
	cd $$(mktemp -d) \
		&& wget -q -O certdata.txt "http://mxr.mozilla.org/seamonkey/source/security/nss/lib/ckfw/builtins/certdata.txt?raw=1" \
		&& md5sum certdata.txt \
		&& mozroots --import --sync --file certdata.txt

Fw-build-package: InstallCerts
	cd $(BUILD_ROOT)/Build \
		&& xbuild '/t:build4package;zipLocalizedLists;localize' /property:config=release /property:packaging=yes

Fw-build-package-fdo: InstallCerts
	cd $(BUILD_ROOT)/Build \
		&& xbuild '/t:build4package-fdo' /property:config=release /property:packaging=yes

TE-run: ComponentsMap-nodep
	(. ./environ && cd $(OUT_DIR) && mono --debug TE.exe -db "$${TE_DATABASE}")

# Begin localization section

LOCALIZATIONS := $(shell ls $(BUILD_ROOT)/Localizations/messages.*.po | sed 's/.*messages\.\(.*\)\.po/\1/')

l10n-all:
	(cd $(BUILD_ROOT)/Build && xbuild /t:localize)

l10n-clean:
	# We don't want to remove strings-en.xml
	for LOCALE in $(LOCALIZATIONS); do \
		rm -rf "$(BUILD_ROOT)/Output/{Debug,Release}/$$LOCALE" "$(BUILD_ROOT)/DistFiles/Language Explorer/Configuration/strings-$$LOCALE.xml" ;\
	done

l10n-install:
	install -d $(DESTDIR)/usr/lib/fieldworks
	install -d "$(DESTDIR)/usr/share/fieldworks/Language Explorer/Configuration"
	for LOCALE in $(LOCALIZATIONS); do \
		DESTINATION=$(DESTDIR)/usr/lib/fieldworks-l10n-$${LOCALE,,} ;\
		install -d $$DESTINATION ;\
		install -m 644 Output/Release/$$LOCALE/*.dll $$DESTINATION/ ;\
		install -m 644 "$(BUILD_ROOT)/DistFiles/Language Explorer/Configuration/strings-$$LOCALE.xml" $$DESTINATION/ ;\
		ln -sf ../fieldworks-l10n-$${LOCALE,,} $(DESTDIR)/usr/lib/fieldworks/$$LOCALE ;\
		ln -sf ../../../../lib/fieldworks-l10n-$${LOCALE,,}/strings-$$LOCALE.xml "$(DESTDIR)/usr/share/fieldworks/Language Explorer/Configuration/strings-$$LOCALE.xml" ;\
	done

# End localization section
