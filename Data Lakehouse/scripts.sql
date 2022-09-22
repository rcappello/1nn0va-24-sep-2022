--this is the watermark table
select * from theshire.watermarktable

--this is the device table
select * from theshire.devices

--Lobelia messed up some things...
UPDATE THESHIRE.DEVICES
SET Owner = 'theshire\lobelia', OwnerEmail = 'lobelia@theshire.com',
ModifiedDate = GETDATE()
where GreenhouseCode = 'GREENH2'



--Restores Frodo as owner
UPDATE THESHIRE.DEVICES
SET OWNER = 'theshire\frodo', OwnerEmail = 'Frodo@theShire.com',
ModifiedDate = '2022-01-01'
where GreenhouseCode = 'GREENH2'

--Restores watermark table to start from the beginning
update theshire.watermarktable
set watermarkvalue = '2020-01-01'

