В процессе маркировки данные из этикетировщика записываются в БД BCT2DB, расположенную на сервере BRAIN2 (Bizerba Rapid Application Industry Network) 
Имя таблицы для записи данных соответствует имени настроенного в BRAIN2 фильтра (в нашем случае Filter2)
---------------------------------------------------------------------------------------------------------------------
папка приложения C:\DIAR\BizerbaClient

bizutil -put - загрузка КМ bp из БД Диармарк в этикетировщик Bizerba
-чтение номера заказа из файла self.txt
-чтение кодов по заказу из БД Диармарк (РМС)
-генерация файла с КМ для загрузки (временный файл создается в папке C:\Users\текущий_юзерь\AppData\Local\Temp)
-загрузка файла в этикетировщик

bizutil -get - обновление данных по весу в БД Диармарк
-чтение данных (КМ, вес) из БД BCT2DB
-запись файла №заказа.json в папку C:\DIAR\BizerbaClient\JSON

BizerbaUtil.log - простенький журнал работы в папке приложения
---------------------------------------------------------------------------------------------------------------------
формат .JSON файла:
[{"Weight":"400","Code":"010461045339137021123456\u001D93aDSw"},{"Weight":"410","Code":"010461045339137021234567\u001D93)iOp"},{"Weight":"420","Code":"010461045339137021345678\u001D932eR4"},{"Weight":"430","Code":"010461045339137021456789\u001D93tGHY"},{"Weight":"440","Code":"010461045339137021567890\u001D93ssdd"},{"Weight":"450","Code":"010461045339137021098765\u001D93fWEr"},{"Weight":"460","Code":"010461045339137021987654\u001D935tTg"},{"Weight":"470","Code":"010461045339137021876543\u001D937kKl"},{"Weight":"480","Code":"010461045339137021765432\u001D93dGVz"},{"Weight":"490","Code":"010461045339137021)Rai:H\u001D93dGVz"}]
---------------------------------------------------------------------------------------------------------------------
Настройки BizUtil.dll.config:

DmQuery - запрос КМ из БД Диармарк
BcsQuery - запрос КМ и данных веса из БД Bizerba (BCT2DB)
ftpServer - IP этикетировщика
FtpPort - FTP порт этикетировщика
DmWorkFolder - рабочая папка диармарк

DmDbConnection - строка подключения в БД Диармарк
Bct2DbConnection - строка подключения в БД Bizerba
---------------------------------------------------------------------------------------------------------------------		
<?xml version="1.0" encoding="utf-8" ?>  
<configuration>  
    <appSettings>
		<add key="DmQuery" value="SELECT kod_KM from sklad AS s LEFT JOIN orders as o ON s.id_orders=o.id WHERE id_global_orders=@ID_ORDER" />
        <add key="BcsQuery" value="SELECT INSERT_TIMEDATA, DEVICE,GL19, PD00, GT32, GT61, GT62, GT63 FROM Filter2 WHERE INSERT_TIMEDATA >= CAST(GETDATE() as DATE) AND GT32=@GTIN" />
		<add key="ftpServer" value="10.5.11.151" />
		<add key="FtpPort" value="21" />
		<add key="DmWorkFolder" value="C:\DIAR\DIARMARK" />
		<add key="CurrentGTIN" value="04610453391370" />
	</appSettings>  
    <connectionStrings>
		<add name="DmDbConnection" connectionString="Server='SMYARMED';Database='crux';User Id='diar';Password='123';Trusted_Connection=True;TrustServerCertificate=True;Integrated Security=False;"/>
		<!--add name="Bct2DbConnection" connectionString="Server='sgisov3\SQLEXPRESS';Database='BCT2DB';User Id='sa';Password='Qwerty12345678';"/-->
		<add name="Bct2DbConnection" connectionString="Server='SMYARMED';Database='BCT2DB';User Id='diar';Password='123';;Trusted_Connection=True;TrustServerCertificate=True;Integrated Security=False;"/>
	</connectionStrings>  
</configuration>