<?php
/*
Template Name: Zeitaku Leaderboard
*/
get_header();

// Define the URL and parameters
$url = "https://2B1A2.playfabapi.com/Client/LoginWithCustomID";
$params = array(
    "TitleId" => "2B1A2",
    "CustomId" => "62B5B8E06D5432",
    "CreateAccount" => true
);
// Encode the parameters as JSON
$data = json_encode($params);
// Initialize cURL
$ch = curl_init();
// Set the options
curl_setopt($ch, CURLOPT_URL, $url);
curl_setopt(
    $ch,
    CURLOPT_HTTPHEADER,
    array(
        "Content-Type: application/json"
    )
);
curl_setopt($ch, CURLOPT_POST, true);
curl_setopt($ch, CURLOPT_POSTFIELDS, $data);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
// Execute the request and get the response
$response = curl_exec($ch);
// Close cURL
curl_close($ch);
// Decode the response as JSON
$result = json_decode($response, true);
// Get the session ticket from the result
$session_ticket = $result["data"]["SessionTicket"];
// Print the session ticket
//var_dump($session_ticket);


$playFabUrl = 'https://2B1A2.playfabapi.com/';
$secretKey = 'REDACTED';

/// Define the URL and parameters
$url = "https://2B1A2.playfabapi.com/Client/GetLeaderboard";
$params = array(
    "StatisticName" => "Zeitaku_Leaderboard",
    "StartPosition" => 1,
    "MaxResultsCount" => 100
);
// Encode the parameters as JSON
$data = json_encode($params);
// Initialize cURL
$ch = curl_init();
// Set the options
curl_setopt($ch, CURLOPT_URL, $url);
curl_setopt(
    $ch,
    CURLOPT_HTTPHEADER,
    array(
        "Content-Type: application/json",
        "X-Authorization: $session_ticket"
    )
);
curl_setopt($ch, CURLOPT_POST, true);
curl_setopt($ch, CURLOPT_POSTFIELDS, $data);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
// Execute the request and get the response
$response = curl_exec($ch);
// Close cURL
curl_close($ch);
// Decode the response as JSON
$result = json_decode($response, true);
// Print the result
//print_r($result);


if ($result) {

    ?>

    <table class="d-leaderboard">
        <caption>"Enter the Dragon" Honor Leaderboard</caption>
        <tr>
            <th>Rank</th>
            <th>Name</th>
            <th>Honor</th>
        </tr>

        <?php
        foreach ($result['data']['Leaderboard'] as $value) {


            echo '<tr>
                   <td>' . $value['Position'] . '</td>
                   <td>' . $value['DisplayName'] . '</td>
                   <td>' . $value['StatValue'] . '</td>
               </tr> '; # code...
        }
        ?>
     
    </table>
    <?php
}


get_footer();

?>

