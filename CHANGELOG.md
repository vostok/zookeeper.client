## 0.1.15 (16-12-2024): 

Bump NuGet deps versions

## 0.1.14 (14-04-2022):

- Implemented escaping of ZooKeeperNetEx client log messages
- Added `net6.0` target of ZooKeeperNetEx client. 

## 0.1.13 (06-12-2021):

Added `net6.0` target.

## 0.1.11 (12-10-2021):

Suspend client on empty connection string.

## 0.1.10 (28-02-2021):

Classify `VersionsMismatch` status as warning.

## 0.1.9 (18-02-2021):

- Temporary fix for .NET Core and .NET5 bug with ConnectionResetException.

- Added tracing.

## 0.1.8 (05-11-2020):

Return rename internalize option for ilrepack.

## 0.1.6 (03-11-2020):

Implemented IZooKeeperAuthClient interface.

## 0.1.5 (21-08-2020):

Added `SessionTimeout` field.


## 0.1.4 (08-07-2020):

Added delays between connecting to ZooKeeper cluster if these attempts fail repeatedly (`MaximumConnectPeriodMultiplier` setting).

## 0.1.3 (16-03-2020):

Suppress context flow.

## 0.1.2 (04-03-2020):

Added `Died` status.

## 0.1.1 (14-05-2019): 

ZooKeeperClientSettings takes IList<Uri> instead of Uri[] replicas.

## 0.1.0 (21-03-2019): 

Initial prerelease.