# Drivers — expansão consultiva de hardware (22/11/2026)

Somente Drivers → Investigar. URL de suporte, driver instalado, canal, oferta WUA e elegibilidade de pacote não são equivalentes; nenhuma instalação é habilitada por este catálogo.

Qualcomm QCA9377 (PCI 168C:0042): https://www.qualcomm.com/wi-fi/products/qca9377/support ; correspondência PCI publicada em https://learn.microsoft.com/en-us/answers/questions/3826493/device-pciven-168c-dev-0042-subsys-090117aa-rev-31 ; política de suporte OEM: https://www.qualcomm.com/drivers
Qualcomm 168C e 17CB sem modelo exato: https://www.qualcomm.com/drivers (fabricante apenas).
MediaTek MT7921 (PCI 14C3:7961): https://support.lenovo.com/br/pt/downloads/ds552761-mediatek-wlan-driver-for-windows-10-version-20h2-or-later-thinkpad-e14-gen-3-e15-gen-3 ; https://www.mediatek.com/press-room/mediatek-wi-fi-6-chipset-powers-new-asus-gaming-notebooks
MediaTek MT7922 / RZ616 (PCI 14C3:0616): https://learn.microsoft.com/es-mx/answers/questions/5976911/problema-con-rz616-wi-fi-6e-msi-mag-b650m-mortar-w ; OEM Lenovo/Dell/HP/Acer deve prevalecer se o SUBSYS for válido.
MediaTek sem variante OEM: https://www.mediatek.com/products/networking-and-connectivity (institucional, não catálogo Windows).
NVIDIA Fermi GTS 450 (10DE:0DC4) e GTX 460 (10DE:0E22): https://www.nvidia.com/pt-br/drivers/details/132985/ ; IDs também vistos em https://www.catalog.update.microsoft.com/ScopedViewInline.aspx?updateid=5b10eefc-792a-4129-bf05-c8ad4a6ff691 e https://www.catalog.update.microsoft.com/ScopedViewInline.aspx?updateid=2c2892a4-e014-4df7-ad7c-0c29dc61f4ac . Página histórica, não atualização confirmada.
AMD Radeon HD 5000/Cedar (1002:68F9): https://www.amd.com/en/resources/support-articles/faqs/GPU-630.html ; modelo comercial a confirmar.
Intel HD Graphics 3000 (8086:0126): https://www.intel.com/content/www/us/en/support/articles/000005526/graphics.html (matriz de sistemas, não pacote Windows 11).
DUALSHOCK 4 (054C:05C4 e 054C:09CC): https://www.playstation.com/pt-br/support/hardware/ps4-pair-dualshock-4-wireless-with-pc-or-mac/
Steam Controller 2015 (28DE:1102): https://help.steampowered.com/pt-br/faqs/view/1796-5FC3-88B3-C85F ; não confundir com modelo de 2026.
Xbox 360 USB (045E:028E): https://www.catalog.update.microsoft.com/Search.aspx?q=USB%5CVID_045E%26PID_028E ; catálogo histórico, sem atestar elegibilidade do Windows atual.

Guardas: sem aceitar SUBSYS conflitante/malformado, CompatibleIds como OEM, classe divergente ou inventário parcial. Testes dirigidos em TestesDrivers/DriverHardwareSupportTests.cs. Homologação de hardware real e consulta de portais OEM continuam pendentes.
