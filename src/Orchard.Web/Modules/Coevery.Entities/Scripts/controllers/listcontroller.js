﻿'use strict';

define(['core/app/detourService', 'Modules/Coevery.Entities/Scripts/services/entitydataservice'], function (detour) {
    detour.registerController([
      'EntityListCtrl',
      ['$rootScope', '$scope', 'logger', '$detour', '$resource', '$stateParams', 'entityDataService',
      function ($rootScope, $scope, logger, $detour, $resource, $stateParams, entityDataService) {
          var t = function (str) {
              var result = i18n.t(str);
              return result;
          };

          var metadataColumnDefs = [
              { name: 'Id', label: 'Id', hidden: true },
              {
                  name: 'DisplayName', label: t('Display Name'), width: 450,
                  formatter: $rootScope.cellLinkTemplate,
                  formatoptions: { hasView: true }
              },
              { name: 'IsDeployed', label: t('Is Deployed'), width: 450, }];

          $scope.gridOptions = {
              url: "api/entities/entity",
              colModel: metadataColumnDefs,
          };

          angular.extend($scope.gridOptions, $rootScope.defaultGridOptions);

          $scope.delete = function (entityName) {
              $scope.entityName = entityName;
              $('#myModalEntity').modal({
                  backdrop: 'static',
                  keyboard: true
              });
          };

          $scope.deleteEntity = function () {
              $('#myModalEntity').modal('hide');
              entityDataService.delete({ name: $scope.entityName }, function () {
                  if ($scope.selectedItems.length != 0) {
                      $scope.selectedItems.pop();
                  }
                  $scope.getAllMetadata();
                  logger.success("Delete the entity successful.");
              }, function (reason) {
                  logger.error("Failed to delete the entity:" + reason);
              });
          };

          $scope.add = function () {
              $detour.transitionTo('EntityCreate', { Module: 'Entities' });
          };

          $scope.view = function (entityName) {
              $detour.transitionTo('EntityDetail.Fields', { Id: entityName });
          };

          $scope.edit = function (entityName) {
              $detour.transitionTo('EntityEdit', { Id: entityName });
          };

          $scope.getAllMetadata = function () {
              $("#gridList").jqGrid('setGridParam', {
                  datatype: "json"
              }).trigger('reloadGrid');
          };
      }]
    ]);
});