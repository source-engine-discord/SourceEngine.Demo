
<!DOCTYPE html>
<html>
<head>
    <title>page</title>
    <script src="resources/jquery-1.12.4.js"></script>
    <script src="resources/jquery.dataTables.min.js"></script>

    <script>
      $(document).ready(function() {
          $('#csv_target').DataTable( {
              "scrollY":        "800px",
              "scrollCollapse": true,
              "paging":         true
          } );
      } );
    </script>

    <link rel="stylesheet" type="text/css" href="resources/tablestyle.css">
</head>
<body>
  <?php

  require ('secret/steamconfig.php');

  $rows = array(array());
  $steamids = array();

  $row = 1;
  if(($handle = fopen("data.csv", "r")) !== FALSE){
    while(($data = fgetcsv($handle, 1000, ",")) !== FALSE) {
      $num = count($data);
      //Steam id 934724F3CF2590AE784AF4BAD539F18E

      $row++;

      array_push($rows, $data);
      if($row > 2)
        array_push($steamids, $data[0]);
    }

    fclose($handle);

    $url = file_get_contents("http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=".$apikey."&steamids=".join('-', $steamids));

    $content = json_decode($url, true);

    //Create table
    echo "<table border='2' id='csv_target' class='dataTable cell-border order-column stripe' cellspacing='0' width='100%' style='width: 100%'>";

    //cellspacing="0" width="100%" role="grid" aria-describedby="example_info" style="width: 100%;"

    echo "<thead><tr><td>Player</td>";
    for($x=1; $x < count($rows[1]); $x++)
    {
      echo "<td>".$rows[1][$x]."</td>";
    }

    echo "</tr></thead><tbody>";

    for($i=2; $i < count($rows); $i++)
    {
      echo "<tr>";
      for($c=0; $c < count($content['response']['players']); $c++)
      {
        if($content['response']['players'][$c]['steamid'] == $rows[$i][0])
        {
          echo "<td><img src=".$content['response']['players'][$c]['avatar']."/><p>".$content['response']['players'][$c]['personaname'].'</p></td>';
          break;
        }
      }

      for($x=1; $x < count($rows[$i]); $x++)
      {
        echo "<td>".$rows[$i][$x]."</td>";
      }
      echo "</tr>";
    }

    echo "</tbody></table>";

    //for($p=0; $p < sizeof($content['response']['players']); $p++){
      //echo "<img src=".$content['response']['players'][$p]['avatarmedium']."/>";
    //}
  }

   ?>
</body>
</html>
<!--Version 3.2-->
